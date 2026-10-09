using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Tools that go from one scene to another as the game's buttons do: launch a vessel, revert a flight,
    /// recover a vessel, fly one from the tracking station, leave for another scene.
    /// </summary>
    internal static class SceneTools
    {
        private static readonly Dictionary<string, GameScenes> Scenes = new Dictionary<string, GameScenes>
        {
            { "SPACECENTER", GameScenes.SPACECENTER },
            { "TRACKSTATION", GameScenes.TRACKSTATION },
            { "MAINMENU", GameScenes.MAINMENU }
        };

        private static readonly Dictionary<string, EditorFacility> Editors = new Dictionary<string, EditorFacility>
        {
            { "VAB", EditorFacility.VAB },
            { "SPH", EditorFacility.SPH }
        };

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("launch_vessel",
                "Launches a vessel from its .craft file at a launch site, as the editor's Launch button does, with " +
                "the crew the editor would give it by default, and waits until physics runs on it. KSP saves the " +
                "game as 'persistent' first, as it does for any launch. Needs a game loaded. Refuses a craft " +
                "whose parts are not all unlocked in this game, where the launch dialog of the space centre asks " +
                "for a confirmation.",
                Schema.Object(
                    Schema.P("craft", "string",
                        "the .craft file: absolute, or relative to the Ships folder of the game being played " +
                        "(SPH/<name>.craft or VAB/<name>.craft)", true),
                    Schema.P("site", "string", "the launch site's name (default Runway; LaunchPad for the pad)")),
                LaunchVessel);
            yield return new Tool("revert_to_launch",
                "Reverts the flight to its launch, as Revert to Launch does, and waits until physics runs on " +
                "the vessel again.",
                Schema.Object(), RevertToLaunch);
            yield return new Tool("revert_to_editor",
                "Reverts the flight to the editor it was launched from, as Revert to Vehicle Assembly Building " +
                "or Revert to Space Plane Hangar does, and waits until the editor is up.",
                Schema.Object(
                    Schema.P("facility", "string", "VAB or SPH (default VAB)")),
                RevertToEditor);
            yield return new Tool("recover_vessel",
                "Recovers the active vessel, as the Recover button above the altimeter does: KSP saves the game, " +
                "goes to the space centre and recovers the vessel there. Waits until the recovery report is up, " +
                "and answers with the place and the share of the value it gives (location, factor).",
                Schema.Object(), RecoverVessel);
            yield return new Tool("go_to_scene",
                "Leaves the flight, or the scene the game is in, for the space centre, the tracking station or " +
                "the main menu, saving the game as persistent first as the game's own buttons do, and waits until " +
                "that scene is up.",
                Schema.Object(
                    Schema.P("scene", "string", "SPACECENTER, TRACKSTATION or MAINMENU", true)),
                GoToScene);
            yield return new Tool("fly_vessel",
                "Flies a vessel from the tracking station, as its Fly button does: KSP saves the game as " +
                "persistent and opens the flight on that vessel. Waits until physics runs on it. Only a vessel " +
                "the player owns, not an asteroid.",
                Schema.Object(
                    Schema.P("vessel", "string", "the vessel's id, or its name when no other vessel bears it", true)),
                FlyVessel);
        }

        private static IEnumerator LaunchVessel(ToolCall call)
        {
            if (!Require.Game(call))
            {
                yield break;
            }
            string path = call.String("craft");
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(GameTools.SavePath(HighLogic.SaveFolder, "Ships"), path);
            }
            path = Path.GetFullPath(path);
            ConfigNode craft = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (craft == null)
            {
                call.Fail("Could not read the craft " + path);
                yield break;
            }

            // What the launch dialog of the space centre reads of the craft. Where it asks for a confirmation
            // before launching a craft whose parts are not all unlocked, this refuses.
            KSP.UI.Screens.CraftProfileInfo profile = new KSP.UI.Screens.CraftProfileInfo().LoadDetailsFromCraftFile(craft, path);
            if (!profile.shipPartsUnlocked || !profile.shipPartModulesAvailable)
            {
                call.Fail("The craft cannot be launched as it is in this game: " + profile.GetErrorMessage());
                yield break;
            }
            string site = call.String("site", "Runway");
            string limits = LaunchLimitsBroken(profile, site);
            if (limits != null)
            {
                call.Fail("The editor would not launch this craft: " + limits);
                yield break;
            }

            // The crew the editor proposes for that craft, without hiring anyone.
            VesselCrewManifest crew = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(craft, null, false);
            Vessel previous = FlightGlobals.ActiveVessel;
            FlightDriver.StartWithNewLaunch(path, HighLogic.CurrentGame.flagURL, site, crew);
            yield return Waits.ForNewActiveVessel(call, previous);
        }

        /// <summary>
        /// Why the editor's Launch button would refuse a craft at a launch site of the space centre — too heavy or
        /// too large for the site's level, too many parts for its editor's — or null when it would not. Sites other
        /// than the launchpad and the runway are not checked.
        /// </summary>
        private static string LaunchLimitsBroken(KSP.UI.Screens.CraftProfileInfo profile, string site)
        {
            SpaceCenterFacility facility;
            if (string.Equals(site, "LaunchPad", StringComparison.OrdinalIgnoreCase))
            {
                facility = SpaceCenterFacility.LaunchPad;
            }
            else if (string.Equals(site, "Runway", StringComparison.OrdinalIgnoreCase))
            {
                facility = SpaceCenterFacility.Runway;
            }
            else
            {
                return null;
            }
            bool isPad = facility == SpaceCenterFacility.LaunchPad;
            bool isVAB = profile.shipFacility != EditorFacility.SPH;
            float siteLevel = ScenarioUpgradeableFacilities.GetFacilityLevel(facility);
            float editorLevel = ScenarioUpgradeableFacilities.GetFacilityLevel(
                isVAB ? SpaceCenterFacility.VehicleAssemblyBuilding : SpaceCenterFacility.SpaceplaneHangar);

            // The tests of the editor, with the limits it reads.
            PreFlightTests.IPreFlightTest[] tests =
            {
                new PreFlightTests.CraftWithinMassLimits(profile.totalMass, profile.shipName, facility,
                    GameVariables.Instance.GetCraftMassLimit(siteLevel, isPad)),
                new PreFlightTests.CraftWithinSizeLimits(profile.shipSize, profile.shipName, facility,
                    GameVariables.Instance.GetCraftSizeLimit(siteLevel, isPad)),
                new PreFlightTests.CraftWithinPartCountLimit(profile.partCount,
                    isVAB ? SpaceCenterFacility.VehicleAssemblyBuilding : SpaceCenterFacility.SpaceplaneHangar,
                    GameVariables.Instance.GetPartCountLimit(editorLevel, isVAB))
            };
            List<string> broken = new List<string>();
            foreach (PreFlightTests.IPreFlightTest test in tests)
            {
                if (!test.Test())
                {
                    broken.Add(test.GetWarningTitle() + ": " + test.GetWarningDescription());
                }
            }
            return broken.Count > 0 ? string.Join("; ", broken.ToArray()) : null;
        }

        private static IEnumerator RevertToLaunch(ToolCall call)
        {
            if (!Require.Flight(call))
            {
                yield break;
            }
            if (!FlightDriver.CanRevertToPostInit || FlightDriver.PostInitState == null)
            {
                call.Fail("This flight cannot be reverted to its launch");
                yield break;
            }
            Vessel previous = FlightGlobals.ActiveVessel;
            FlightDriver.RevertToLaunch();
            // What the buttons of the flight results dialog do after theirs, the dialog being up after a crash.
            KSP.UI.Dialogs.FlightResultsDialog.Close();
            yield return Waits.ForNewActiveVessel(call, previous);
        }

        private static IEnumerator RevertToEditor(ToolCall call)
        {
            if (!Require.Flight(call))
            {
                yield break;
            }
            EditorFacility facility;
            if (!call.Choice("facility", "VAB", Editors, out facility))
            {
                yield break;
            }
            if (!FlightDriver.CanRevertToPrelaunch || FlightDriver.PreLaunchState == null)
            {
                call.Fail("This flight cannot be reverted to an editor");
                yield break;
            }
            SceneWatch watch = SceneWatch.Start(GameScenes.EDITOR);
            FlightDriver.RevertToPrelaunch(facility);
            // What the buttons of the flight results dialog do after theirs, the dialog being up after a crash.
            KSP.UI.Dialogs.FlightResultsDialog.Close();
            yield return Waits.ForScene(call, watch);
            if (!call.IsError)
            {
                call.Text(GameState.State());
            }
        }

        private static IEnumerator RecoverVessel(ToolCall call)
        {
            if (!Require.Flight(call))
            {
                yield break;
            }
            Vessel v = FlightGlobals.ActiveVessel;
            if (!v.IsRecoverable)
            {
                call.Fail("The active vessel cannot be recovered");
                yield break;
            }
            // The button refuses for the same reasons: moving, on a ladder, about to crash...
            ClearToSaveStatus status = FlightGlobals.ClearToSave();
            if (status != ClearToSaveStatus.CLEAR)
            {
                call.Fail("KSP refuses to recover now: " + status);
                yield break;
            }
            SceneWatch watch = SceneWatch.Start(GameScenes.SPACECENTER);
            GameEvents.OnVesselRecoveryRequested.Fire(v);
            yield return Waits.ForScene(call, watch);
            if (call.IsError)
            {
                yield break;
            }

            // The vessel is recovered once the space centre is up, and the report follows.
            KSP.UI.Screens.MissionRecoveryDialog report = null;
            yield return Waits.Until(call,
                () => (report = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.MissionRecoveryDialog>()) != null,
                30f, "The space centre is up, but no recovery report after 30 seconds");
            if (call.IsError)
            {
                yield break;
            }
            Dictionary<string, object> state = GameState.State();
            state["recovery"] = new Dictionary<string, object>
            {
                { "location", report.recoveryLocation },
                { "factor", report.recoveryFactor }
            };
            call.Text(state);
        }

        private static IEnumerator GoToScene(ToolCall call)
        {
            GameScenes scene;
            if (!call.Choice("scene", null, Scenes, out scene) || !Require.Game(call))
            {
                yield break;
            }
            // What the buttons above the altimeter do in flight, and what leaving any other scene does. Quit
            // to Main Menu, in flight, also forgets the vessels' persistent ids before saving (internal).
            if (scene == GameScenes.MAINMENU && HighLogic.LoadedSceneIsFlight)
            {
                MethodInfo clearIds = typeof(FlightGlobals).GetMethod("ClearpersistentIdDictionaries",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (clearIds == null)
                {
                    call.Fail("FlightGlobals.ClearpersistentIdDictionaries not found in this version of KSP");
                    yield break;
                }
                clearIds.Invoke(null, null);
            }
            SceneWatch watch = SceneWatch.Start(scene);
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            HighLogic.LoadScene(scene);
            // What the buttons of the flight results dialog do after theirs, the dialog being up after a crash.
            KSP.UI.Dialogs.FlightResultsDialog.Close();
            yield return Waits.ForScene(call, watch);
            if (!call.IsError)
            {
                call.Text(GameState.State());
            }
        }

        private static IEnumerator FlyVessel(ToolCall call)
        {
            if (!Require.Scene(call, GameScenes.TRACKSTATION, "Vessels are flown from the tracking station"))
            {
                yield break;
            }
            string error;
            Vessel vessel = Lookup.FindVessel(call.String("vessel"), out error, true);
            if (vessel == null)
            {
                call.Fail(error);
                yield break;
            }
            if (vessel.DiscoveryInfo.Level != DiscoveryLevels.Owned)
            {
                call.Fail(vessel.vesselName + " is not the player's: the tracking station does not fly it");
                yield break;
            }
            // What the Fly button does for a vessel of the player's.
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            FlightDriver.StartAndFocusVessel("persistent", FlightGlobals.Vessels.IndexOf(vessel));
            yield return Waits.ForNewActiveVessel(call, null);
        }
    }
}
