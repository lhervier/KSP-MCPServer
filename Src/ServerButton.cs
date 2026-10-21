using System;
using KSP.UI.Screens;
using UnityEngine;

namespace com.github.lhervier.ksp.util.mcpserver
{
    /// <summary>
    /// The button of the server in the toolbar of KSP, in every scene that has the toolbar: pressed and green
    /// while the server listens, grey while it does not. Clicking it asks to turn the server on or off.
    /// </summary>
    internal sealed class ServerButton
    {
        private const int IconSize = 38;

        private readonly Callback _turnOn;
        private readonly Callback _turnOff;
        private ApplicationLauncherButton _button;
        private Texture2D _onIcon;
        private Texture2D _offIcon;
        private bool _on;

        public ServerButton(Callback turnOn, Callback turnOff)
        {
            _turnOn = turnOn;
            _turnOff = turnOff;
        }

        /// <summary>Puts the button in the toolbar, when it is not there yet. For onGUIApplicationLauncherReady.</summary>
        public void OnLauncherReady()
        {
            // The toolbar lives across scenes, and keeps the buttons of mods with it.
            if (_button != null || ApplicationLauncher.Instance == null)
            {
                return;
            }
            if (_onIcon == null)
            {
                _onIcon = Icon(new Color(0.35f, 0.95f, 0.35f), true);
                _offIcon = Icon(new Color(0.6f, 0.6f, 0.6f), false);
            }
            try
            {
                _button = ApplicationLauncher.Instance.AddModApplication(_turnOn, _turnOff, null, null, null, null,
                    ApplicationLauncher.AppScenes.ALWAYS, _on ? _onIcon : _offIcon);
            }
            catch (Exception e)
            {
                Log.Error("Could not add the toolbar button: " + e.Message);
                return;
            }
            Show(_on);
        }

        /// <summary>Forgets the button, gone with the toolbar. For onGUIApplicationLauncherDestroyed.</summary>
        public void OnLauncherDestroyed()
        {
            _button = null;
        }

        /// <summary>Shows whether the server listens, without asking to turn it on or off.</summary>
        public void Show(bool on)
        {
            _on = on;
            if (_button == null)
            {
                return;
            }
            _button.SetTexture(on ? _onIcon : _offIcon);
            if (on)
            {
                _button.SetTrue(false);
            }
            else
            {
                _button.SetFalse(false);
            }
        }

        // A ring of the colour on a transparent background: with a dot in its centre when on is true, struck
        // out by a diagonal bar when it is false.
        private static Texture2D Icon(Color color, bool on)
        {
            Texture2D texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            float centre = (IconSize - 1) / 2f;
            const float ringOuter = 15f, ringInner = 11.5f, dot = 7f, barHalfWidth = 1.75f;
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    float dx = x - centre, dy = y - centre;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // How much of the pixel each shape covers, over half a pixel of edge, for smooth borders.
                    float ring = Mathf.Clamp01(ringOuter + 0.5f - d) * Mathf.Clamp01(d - ringInner + 0.5f);
                    float inside;
                    if (on)
                    {
                        inside = Mathf.Clamp01(dot + 0.5f - d);
                    }
                    else
                    {
                        // The bar runs from top left to bottom right (rows count from the bottom), inside the ring.
                        float fromBar = Mathf.Abs(dx + dy) / Mathf.Sqrt(2f);
                        inside = Mathf.Clamp01(barHalfWidth + 0.5f - fromBar) * Mathf.Clamp01(ringInner + 0.5f - d);
                    }
                    Color pixel = color;
                    pixel.a = Mathf.Max(ring, inside);
                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
