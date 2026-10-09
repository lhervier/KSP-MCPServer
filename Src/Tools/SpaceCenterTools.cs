using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Tools that click the space centre: its buildings, their menus, the research of the tech tree, and what
    /// is left open over a scene.
    /// </summary>
    internal static class SpaceCenterTools
    {
        /// <summary>The buttons of a building's menu, by their name in the tool, with the name of their field.</summary>
        private static readonly Dictionary<string, string> MenuButtons = new Dictionary<string, string>
        {
            { "Upgrade", "UpgradeButton" },
            { "Repair", "RepairButton" }
        };

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("open_facility",
                "Clicks a building of the space centre, as the left mouse button does: the Vehicle Assembly " +
                "Building and the Space Plane Hangar open their editor, the Tracking Station its scene, the other " +
                "buildings their screen over the space centre, or the dialog KSP shows when it is closed in this " +
                "game. Waits until the scene it opens is up, or a second for a screen. Answers with the scene " +
                "the game is in.",
                Schema.Object(
                    Schema.P("facility", "string",
                        "the building's facility name, as KSP names it: VAB, SPH, TrackingStation, " +
                        "AstronautComplex, RnD, MissionControl, Administration, LaunchPad, Runway, FlagPole", true)),
                OpenFacility);
            yield return new Tool("facility_menu",
                "Opens the menu of a building of the space centre, as a right click on it does, and reads it: the " +
                "building's level and its number of levels, its damage in percent, the cost of its next upgrade " +
                "and of its repairs, the funds, and the buttons that can be pressed. With a button, presses it as " +
                "the left mouse button does and waits until KSP is done: Upgrade until the new level is built, " +
                "Repair until the repairs are over; then answers with the building as it is. Without, leaves the " +
                "menu open, for a screenshot; close_screen closes it. Fails when the button cannot be pressed, " +
                "and when KSP asks for a confirmation (a vessel standing at the building).",
                Schema.Object(
                    Schema.P("facility", "string",
                        "the building's facility name, as KSP names it: LaunchPad, Runway, VAB, SPH, " +
                        "TrackingStation, AstronautComplex, RnD, MissionControl, Administration", true),
                    Schema.P("button", "string", "Upgrade or Repair; left out, the menu stays open")),
                FacilityMenu);
            yield return new Tool("research_tech",
                "Researches a node of the technology tree, as its Research button in the Research and " +
                "Development screen does: needs that screen open (open_facility RnD), the node researchable (its " +
                "parents researched) and the science it costs. Answers with the science left.",
                Schema.Object(
                    Schema.P("node", "string", "the node's id, as in the tech tree: basicRocketry, generalConstruction...", true)),
                ResearchTech);
            yield return new Tool("close_screen",
                "Closes one thing open over the scene, as its own button does, the topmost first: a dialog " +
                "(its button pressed when it has a single one, such as the guide's of a new career; otherwise " +
                "closed as Escape does), then the flight results shown after a crash, then, at the space centre, " +
                "the menu of a building (as a click beside it " +
                "does), the recovery report, then the screen of a building. Answers with what it closed: closed " +
                "is nothing when nothing was open.",
                Schema.Object(
                    Schema.P("dialogs_only", "boolean",
                        "close a dialog only, never a menu, a report or a screen (default false)")),
                CloseScreen);
        }

        private static IEnumerator OpenFacility(ToolCall call)
        {
            if (!Require.Scene(call, GameScenes.SPACECENTER, "Buildings open from the space centre"))
            {
                yield break;
            }
            SpaceCenterBuilding building = Lookup.FindBuilding(call);
            if (building == null)
            {
                yield break;
            }

            // A building either opens a scene, or a screen over the space centre: a second tells them apart.
            // Recorded from before the click: the scene may be up a few frames after it is asked for.
            SceneRequest request = SceneRequest.Start();
            try
            {
                building.OnLeftClick();
                float start = Time.realtimeSinceStartup;
                while (!request.Loaded)
                {
                    float elapsed = Time.realtimeSinceStartup - start;
                    if (!request.Requested && elapsed >= 1f)
                    {
                        break;
                    }
                    if (elapsed > Waits.SceneSeconds)
                    {
                        call.Fail("The scene " + request.Scene + " was not up after 3 minutes");
                        yield break;
                    }
                    yield return null;
                }
            }
            finally
            {
                request.Stop();
            }
            call.Text(GameState.State());
        }

        private static IEnumerator ResearchTech(ToolCall call)
        {
            RDController controller = RDController.Instance;
            if (controller == null || ResearchAndDevelopment.Instance == null)
            {
                call.Fail("The Research and Development screen is not open (open_facility RnD)");
                yield break;
            }
            string id = call.String("node") ?? "";
            RDNode node = null;
            foreach (RDNode n in controller.nodes)
            {
                if (n != null && n.tech != null && n.tech.techID == id)
                {
                    node = n;
                }
            }
            if (node == null)
            {
                call.Fail("No node " + id + " in the tech tree");
                yield break;
            }
            if (node.IsResearched)
            {
                call.Fail("The node " + id + " is already researched");
                yield break;
            }

            // The screen only offers its Research button on a node whose parents allow it.
            if (node.state != RDNode.State.RESEARCHABLE)
            {
                call.Fail("The node " + id + " cannot be researched yet: its parents are not researched");
                yield break;
            }
            RDTech.OperationResult result = node.tech.ResearchTech();
            controller.techTree.RefreshUI();
            yield return null;
            if (result != RDTech.OperationResult.Successful)
            {
                call.Fail("KSP refused to research " + id + ": " + result + " (" + node.tech.scienceCost +
                    " science, " + ResearchAndDevelopment.Instance.Science + " available)");
                yield break;
            }
            call.Text(new Dictionary<string, object>
            {
                { "node", id },
                { "cost", node.tech.scienceCost },
                { "science", (double)ResearchAndDevelopment.Instance.Science }
            });
        }

        private static IEnumerator FacilityMenu(ToolCall call)
        {
            if (!Require.Scene(call, GameScenes.SPACECENTER, "Building menus open at the space centre"))
            {
                yield break;
            }
            string buttonName = call.String("button");
            string field = null;
            if (!string.IsNullOrEmpty(buttonName) && !call.Choice("button", null, MenuButtons, out field))
            {
                yield break;
            }
            SpaceCenterBuilding building = Lookup.FindBuilding(call);
            if (building == null)
            {
                yield break;
            }

            // Only one menu at a time, as the game shows; the one of this building is opened anew.
            foreach (KSCFacilityContextMenu open in Object.FindObjectsOfType<KSCFacilityContextMenu>())
            {
                open.Dismiss(KSCFacilityContextMenu.DismissAction.None);
            }
            yield return null;
            building.OnRightClick();

            // The menu sets its buttons up once started, and their text and state as it is drawn.
            yield return new WaitForSecondsRealtime(0.5f);
            KSCFacilityContextMenu menu = Object.FindObjectOfType<KSCFacilityContextMenu>();
            if (menu == null)
            {
                call.Fail("The menu of " + building.facilityName + " did not open");
                yield break;
            }
            if (field == null)
            {
                call.Text(FacilityState(building, menu));
                yield break;
            }
            UnityEngine.UI.Button button = MenuButton(menu, field);
            if (!Pressable(button))
            {
                Dictionary<string, object> state = FacilityState(building, menu);
                menu.Dismiss(KSCFacilityContextMenu.DismissAction.None);
                call.Fail("The " + buttonName + " button of " + building.facilityName + " cannot be pressed; the menu " +
                    "reads: " + Json.Write(state));
                yield break;
            }

            // Watched from before the click: an upgrade may be built a few frames after it.
            FacilityWatch watch = new FacilityWatch();
            GameEvents.OnKSCFacilityUpgraded.Add(watch.OnUpgraded);
            try
            {
                button.onClick.Invoke();
                yield return null;
                yield return null;
                PopupDialog[] dialogs = Object.FindObjectsOfType<PopupDialog>();
                if (dialogs.Length > 0)
                {
                    PopupDialog dialog = dialogs[dialogs.Length - 1];
                    call.Fail("KSP asks for a confirmation, left open: " +
                        (dialog.dialogToDisplay != null ? dialog.dialogToDisplay.name : "a dialog"));
                    yield break;
                }
                bool upgrade = field == "UpgradeButton";
                yield return Waits.Until(call, () => upgrade ? watch.Upgraded : building.GetStructureDamage() <= 0f, 120f,
                    buttonName + " of " + building.facilityName + " was not over after 2 minutes");
                if (call.IsError)
                {
                    yield break;
                }
            }
            finally
            {
                GameEvents.OnKSCFacilityUpgraded.Remove(watch.OnUpgraded);
            }

            // The building as its menu reads now, the menu closed again as the click left it.
            yield return new WaitForSecondsRealtime(1f);
            building.OnRightClick();
            yield return new WaitForSecondsRealtime(0.5f);
            menu = Object.FindObjectOfType<KSCFacilityContextMenu>();
            Dictionary<string, object> after = FacilityState(building, menu);
            if (menu != null)
            {
                menu.Dismiss(KSCFacilityContextMenu.DismissAction.None);
            }
            call.Text(after);
        }

        /// <summary>A button of a building's menu, by the name of its field, or null.</summary>
        private static UnityEngine.UI.Button MenuButton(KSCFacilityContextMenu menu, string field)
        {
            FieldInfo info = typeof(KSCFacilityContextMenu).GetField(field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return info != null ? info.GetValue(menu) as UnityEngine.UI.Button : null;
        }

        /// <summary>Whether a button of a menu is shown and can be pressed.</summary>
        private static bool Pressable(UnityEngine.UI.Button button)
        {
            return button != null && button.gameObject.activeInHierarchy && button.interactable;
        }

        /// <summary>
        /// What the menu of a building reads, as a dictionary ready to be written as JSON: its level counted from
        /// 1 as the menu counts it, its number of levels, its damage, the costs, the funds, and the buttons of
        /// <paramref name="menu"/> that can be pressed.
        /// </summary>
        private static Dictionary<string, object> FacilityState(SpaceCenterBuilding building, KSCFacilityContextMenu menu)
        {
            Dictionary<string, object> state = new Dictionary<string, object>
            {
                { "facility", building.facilityName },
                { "damage", (double)building.GetStructureDamage() },
                { "repairCost", (double)building.GetRepairsCost() },
                { "funds", Funding.Instance != null ? Funding.Instance.Funds : double.NaN }
            };
            if (building.Facility != null)
            {
                state["level"] = building.Facility.FacilityLevel + 1;
                state["levels"] = building.Facility.MaxLevel + 1;
                state["upgradeCost"] = (double)building.Facility.GetUpgradeCost();
            }
            if (menu != null)
            {
                List<object> buttons = new List<object>();
                foreach (KeyValuePair<string, string> pair in MenuButtons)
                {
                    if (Pressable(MenuButton(menu, pair.Value)))
                    {
                        buttons.Add(pair.Key);
                    }
                }
                state["buttons"] = buttons;
            }
            return state;
        }

        /// <summary>Whether a building's new level has been built since the watch was made.</summary>
        private sealed class FacilityWatch
        {
            public bool Upgraded;

            public void OnUpgraded(Upgradeables.UpgradeableFacility facility, int level)
            {
                Upgraded = true;
            }
        }

        private static IEnumerator CloseScreen(ToolCall call)
        {
            Dictionary<string, object> answer = new Dictionary<string, object> { { "closed", "nothing" } };
            bool atSpaceCentre = HighLogic.LoadedScene == GameScenes.SPACECENTER;
            PopupDialog[] dialogs = Object.FindObjectsOfType<PopupDialog>();
            MissionRecoveryDialog report = atSpaceCentre ? Object.FindObjectOfType<MissionRecoveryDialog>() : null;
            UISpaceCenter ui = atSpaceCentre ? UISpaceCenter.Instance : null;
            KSCFacilityContextMenu menu = atSpaceCentre ? Object.FindObjectOfType<KSCFacilityContextMenu>() : null;
            bool dialogsOnly = call.Bool("dialogs_only");
            if (dialogs.Length > 0)
            {
                PopupDialog dialog = dialogs[dialogs.Length - 1];
                List<DialogGUIButton> buttons = new List<DialogGUIButton>();
                if (dialog.dialogToDisplay != null && dialog.dialogToDisplay.Options != null)
                {
                    foreach (DialogGUIBase option in dialog.dialogToDisplay.Options)
                    {
                        CollectButtons(option, buttons);
                    }
                }
                answer["closed"] = "dialog";
                answer["dialog"] = dialog.dialogToDisplay != null ? dialog.dialogToDisplay.name : "";
                if (buttons.Count == 1)
                {
                    // What a click on its only button does: its callback, then the dialog closed if the button
                    // closes it and the callback did not already.
                    answer["button"] = buttons[0].OptionText;
                    buttons[0].OptionSelected();
                    yield return null;
                    if (dialog != null && buttons[0].DismissOnSelect)
                    {
                        dialog.Dismiss();
                    }
                }
                else
                {
                    dialog.Dismiss();
                }
            }
            else if (KSP.UI.Dialogs.FlightResultsDialog.isDisplaying)
            {
                // What its Close button does.
                KSP.UI.Dialogs.FlightResultsDialog.Close();
                answer["closed"] = "flight results";
            }
            else if (menu != null && !dialogsOnly)
            {
                // What a click beside it does.
                answer["closed"] = "menu";
                answer["menu"] = menu.name;
                menu.Dismiss(KSCFacilityContextMenu.DismissAction.None);
            }
            else if (report != null && !dialogsOnly)
            {
                // What its button does.
                Object.Destroy(report.gameObject);
                answer["closed"] = "recovery report";
            }
            else if (ui != null && !dialogsOnly)
            {
                string closed = CloseBuildingScreen(ui);
                if (closed != null)
                {
                    answer["closed"] = closed;
                }
            }
            yield return new WaitForSecondsRealtime(1f);
            call.Text(answer);
        }

        /// <summary>
        /// Closes the screen of a building open over the space centre, as its Exit button does; returns the
        /// building's name, or null when no screen is open.
        /// </summary>
        private static string CloseBuildingScreen(UISpaceCenter ui)
        {
            // What the screen's Exit button does, the event Escape fires too.
            if (ui.SpawnedAC)
            {
                GameEvents.onGUIAstronautComplexDespawn.Fire();
                return "AstronautComplex";
            }
            if (ui.SpawnedRD)
            {
                GameEvents.onGUIRnDComplexDespawn.Fire();
                return "RnD";
            }
            if (ui.SpawnedMC)
            {
                GameEvents.onGUIMissionControlDespawn.Fire();
                return "MissionControl";
            }
            if (ui.SpawnedADM)
            {
                GameEvents.onGUIAdministrationFacilityDespawn.Fire();
                return "Administration";
            }
            return null;
        }

        /// <summary>Adds to <paramref name="buttons"/> the buttons of a dialog element and of all it holds.</summary>
        private static void CollectButtons(DialogGUIBase element, List<DialogGUIButton> buttons)
        {
            if (element == null)
            {
                return;
            }
            DialogGUIButton button = element as DialogGUIButton;
            if (button != null)
            {
                buttons.Add(button);
            }
            foreach (DialogGUIBase child in element.children)
            {
                CollectButtons(child, buttons);
            }
        }
    }
}
