﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using MetroFramework;
using MetroFramework.Controls;
using MetroFramework.Forms;

namespace GenshinOverlay {
    public partial class MainWindow : MetroForm {
        private OverlayWindow OverlayWindow;
        private KeyboardHook KeyHook;
        private WindowHook WinHook;

        public MainWindow() {
            InitializeComponent();
        }
        
        private void MainWindow_Load(object sender, EventArgs e) {
            ConfigPanel.Visible = false;

            if(!IsAdmin()) {
                DialogResult res = MetroMessageBox.Show(this, $"\nGenshinOverlay must be started as Administrator.", "Genshin Overlay - Error", MessageBoxButtons.OK, Theme, MessageBoxDefaultButton.Button1, 135);
                if(res == DialogResult.OK) {
                    Environment.Exit(0);
                }
            }

            Config.Load();

            Theme = (MetroThemeStyle)Config.ConfigTheme;
            MStyleManager.Theme = (MetroThemeStyle)Config.ConfigTheme;
            OverlayWindow = new OverlayWindow();

            WinHook = new WindowHook(Config.ProcessName);
            WinHook.WindowHandleChanged += WinHook_WindowHandleChanged;
            
            KeyHook = new KeyboardHook(new List<Keys>() {  Keys.E });
            KeyHook.KeyUp += KeyHook_KeyUp;

            if(Config.CooldownTextLocation == Point.Empty || Config.PartyNumLocation == Point.Empty) {
                ConfigureOverlayMessage.Visible = true;
                ConfigureOverlayButton.Location = new Point(ConfigureOverlayButton.Location.X, ConfigureOverlayButton.Location.Y + 20);
            } else {
                ConfigureOverlayMessage.Visible = false;
            }

            Activate();
            FocusMe();
        }

        public static bool IsAdmin() {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void WinHook_WindowHandleChanged(object sender, WindowEventArgs e) {
            OverlayWindow.CurrentHandle = e.Handle;
            if(e.Handle != IntPtr.Zero) {
                OverlayWindow.GenshinHandle = OverlayWindow.CurrentHandle;
            }
        }

        private void KeyHook_KeyUp(object sender, KeyHookEventArgs e) {
            if(OverlayWindow.GenshinHandle == IntPtr.Zero) { return; }
            if(Config.CooldownTextLocation == Point.Empty || Config.PartyNumLocation == Point.Empty) { return; }
            if(e.Key == Keys.E) {
                if(Party.SelectedCharacter == -1 || Party.Characters[Party.SelectedCharacter].Cooldown > Config.CooldownMinimumReapply || Party.Characters[Party.SelectedCharacter].Processing) { return; }
                int c = Party.SelectedCharacter;
                Party.Characters[c].Processing = true;

                new Thread(() => {
                    Thread.Sleep(100);
                    Point captureLocation = new Point(Config.CooldownTextLocation.X, Config.CooldownTextLocation.Y);
                    Size captureSize = new Size(Config.CooldownTextSize.Width, Config.CooldownTextSize.Height);
                    
                    decimal currentCooldown = IMG.Capture(OverlayWindow.CurrentHandle, captureLocation, captureSize);
                    while(c == Party.SelectedCharacter && currentCooldown == 0) {
                        Thread.Sleep(100);
                        currentCooldown = IMG.Capture(OverlayWindow.CurrentHandle, captureLocation, captureSize);
                    }
                    if(c != Party.SelectedCharacter) {
                        Party.Characters[c].Cooldown = 0;
                        Party.Characters[c].Max = 0;
                    } else {
                        if(Config.CooldownOverride[c] > 0) { 
                            if(currentCooldown < Config.CooldownMinimumOverride) {
                                Party.Characters[c].Cooldown = Config.CooldownOverride[c];
                                Party.Characters[c].Max = Config.CooldownOverride[c];
                            } else {
                                Party.Characters[c].Cooldown = currentCooldown;
                                Party.Characters[c].Max = currentCooldown;
                            }
                        } else {
                            Party.Characters[c].Cooldown = currentCooldown + Config.CooldownOffset;
                            Party.Characters[c].Max = Party.Characters[c].Cooldown;
                        }
                    }
                    Party.Characters[c].Processing = false;
                }).Start();
            }
        }

        private void MainWindow_FormClosed(object sender, FormClosedEventArgs e) {
            KeyHook.Unhook();
            WinHook.Unhook();
            Environment.Exit(0);
        }

        #region "Config":
        private void ConfigureOverlayButton_Click(object sender, EventArgs e) {
            Process proc = Process.GetProcesses().Where(x => x.ProcessName == Config.ProcessName).FirstOrDefault();
            if(proc == null) {
                MetroMessageBox.Show(this, $"\nGenshin Impact must be running first.", "Process Error", MessageBoxButtons.OK, Theme, MessageBoxDefaultButton.Button1, 135);
                return;
            } else {
                OverlayWindow.GenshinHandle = proc.MainWindowHandle;
            }

            User32.GetClientRect(OverlayWindow.GenshinHandle, out User32.RECT rect);
            if(rect.Empty()) {
                MetroMessageBox.Show(this, $"\nGenshin Impact client area could not be detected.\n> Please ensure Genshin Impact is not minimized.\n> Fullscreen is not supported.", "Client Error", MessageBoxButtons.OK, Theme, MessageBoxDefaultButton.Button1, 160);
                return;
            }

            OverlayWindow.IsConfiguring = true;
            if(ConfigureOverlayMessage.Visible) {
                ConfigureOverlayMessage.Visible = false;
                ConfigureOverlayButton.Location = new Point(ConfigureOverlayButton.Location.X, ConfigureOverlayButton.Location.Y - 20);
            }
            ConfigureOverlayButton.Visible = false;
            ConfigPanel.Visible = true;

            CooldownTextXPosTrack.Maximum = rect.Width;
            CooldownTextYPosTrack.Maximum = rect.Height;
            PartyNumXPosTrack.Maximum = rect.Width;
            PartyNumYPosTrack.Maximum = rect.Height;
            CooldownBarsXPosTrack.Maximum = rect.Width;
            CooldownBarsYPosTrack.Maximum = rect.Height;
            CooldownTextXPosTrack.MouseWheelBarPartitions = CooldownTextXPosTrack.Maximum - CooldownTextXPosTrack.Minimum;
            CooldownTextYPosTrack.MouseWheelBarPartitions = CooldownTextYPosTrack.Maximum - CooldownTextYPosTrack.Minimum;
            PartyNumXPosTrack.MouseWheelBarPartitions = PartyNumXPosTrack.Maximum - PartyNumXPosTrack.Minimum;
            PartyNumYPosTrack.MouseWheelBarPartitions = PartyNumYPosTrack.Maximum - PartyNumYPosTrack.Minimum;
            CooldownBarsXPosTrack.MouseWheelBarPartitions = CooldownBarsXPosTrack.Maximum - CooldownBarsXPosTrack.Minimum;
            CooldownBarsYPosTrack.MouseWheelBarPartitions = CooldownBarsYPosTrack.Maximum - CooldownBarsYPosTrack.Minimum;

            if(Config.CooldownTextLocation == Point.Empty || Config.PartyNumLocation == Point.Empty) {
                AssumeDefaultValues(rect);
            }
            UpdateControlValues();
        }

        private void AssumeDefaultValues(User32.RECT rect) {
            Template template = Config.Templates.Find(x => x.Resolution == rect.Size);
            if(template != null) {
                Config.CooldownTextLocation = template.Properties.CooldownTextLocation;
                Config.CooldownTextSize = template.Properties.CooldownTextSize;
                Config.PartyNumLocation = template.Properties.PartyNumLocation;
                Config.PartyNumYOffset = template.Properties.PartyNumYOffset;
                Config.CooldownBarLocation = template.Properties.CooldownBarLocation;
                Config.CooldownBarSize = template.Properties.CooldownBarSize;
                Config.CooldownBarOffset = template.Properties.CooldownBarOffset;
            }
        }

        private void UpdateControlValues() {
            CooldownTextXPosTrack.Value = Config.CooldownTextLocation.X > CooldownTextXPosTrack.Maximum ? CooldownTextXPosTrack.Maximum : Config.CooldownTextLocation.X;
            CooldownTextYPosTrack.Value = Config.CooldownTextLocation.Y > CooldownTextYPosTrack.Maximum ? CooldownTextYPosTrack.Maximum : Config.CooldownTextLocation.Y;
            CooldownTextWidthTrack.Value = Config.CooldownTextSize.Width > CooldownTextWidthTrack.Maximum ? CooldownTextWidthTrack.Maximum : Config.CooldownTextSize.Width;
            CooldownTextHeightTrack.Value = Config.CooldownTextSize.Height > CooldownTextHeightTrack.Maximum ? CooldownTextHeightTrack.Maximum : Config.CooldownTextSize.Height;
            PartyNumXPosTrack.Value = Config.PartyNumLocation.X > PartyNumXPosTrack.Maximum ? PartyNumXPosTrack.Maximum : Config.PartyNumLocation.X;
            PartyNumYPosTrack.Value = Config.PartyNumLocation.Y > PartyNumYPosTrack.Maximum ? PartyNumYPosTrack.Maximum : Config.PartyNumLocation.Y;
            PartyNumYOffsetTrack.Value = Config.PartyNumYOffset > PartyNumYOffsetTrack.Maximum ? PartyNumYOffsetTrack.Maximum : Config.PartyNumYOffset;
            CooldownBarsXPosTrack.Value = Config.CooldownBarLocation.X > CooldownBarsXPosTrack.Maximum ? CooldownBarsXPosTrack.Maximum : Config.CooldownBarLocation.X;
            CooldownBarsYPosTrack.Value = Config.CooldownBarLocation.Y > CooldownBarsYPosTrack.Maximum ? CooldownBarsYPosTrack.Maximum : Config.CooldownBarLocation.Y;
            CooldownBarsWidthTrack.Value = Config.CooldownBarSize.Width > CooldownBarsWidthTrack.Maximum ? CooldownBarsWidthTrack.Maximum : Config.CooldownBarSize.Width;
            CooldownBarsHeightTrack.Value = Config.CooldownBarSize.Height > CooldownBarsHeightTrack.Maximum ? CooldownBarsHeightTrack.Maximum : Config.CooldownBarSize.Height;
            CooldownBarsXOffsetTrack.Value = (int)(Config.CooldownBarOffset.X * 10) > CooldownBarsXOffsetTrack.Maximum ? CooldownBarsXOffsetTrack.Maximum : (int)(Config.CooldownBarOffset.X * 10);
            CooldownBarsYOffsetTrack.Value = (int)(Config.CooldownBarOffset.Y * 10) > CooldownBarsYOffsetTrack.Maximum ? CooldownBarsYOffsetTrack.Maximum : (int)(Config.CooldownBarOffset.Y * 10);
            CooldownBarsModeTrack.Value = Config.CooldownBarMode > CooldownBarsModeTrack.Maximum ? CooldownBarsModeTrack.Maximum : Config.CooldownBarMode;
            CooldownBarsSelOffsetTrack.Value = Config.CooldownBarSelOffset > CooldownBarsSelOffsetTrack.Maximum ? CooldownBarsSelOffsetTrack.Maximum : Config.CooldownBarSelOffset;

            CooldownPropMaxTrack.Value = Config.CooldownMaxPossible;
            CooldownPropOffsetTrack.Value = (int)(Config.CooldownOffset * 10);
            CooldownPropReapplyTrack.Value = (int)(Config.CooldownMinimumReapply * 10);
            CooldownPropOverrideTrack.Value = (int)(Config.CooldownMinimumOverride * 10);
            CooldownPropPauseTrack.Value = (int)(Config.CooldownPauseSubtraction * 10);
            CooldownPropTickTrack.Value = Config.CooldownTickRateInMs;
            CooldownPropConfTrack.Value = (int)(Config.OCRMinimumConfidence * 100);

            FG1ColourText.Text = Config.CooldownBarFG1Color;
            FG2ColourText.Text = Config.CooldownBarFG2Color;
            BGColourText.Text = Config.CooldownBarBGColor;
            SelColourText.Text = Config.CooldownBarSelectedFGColor;
            CooldownOverride1Text.Text = Config.CooldownOverride[0].ToString();
            CooldownOverride2Text.Text = Config.CooldownOverride[1].ToString();
            CooldownOverride3Text.Text = Config.CooldownOverride[2].ToString();
            CooldownOverride4Text.Text = Config.CooldownOverride[3].ToString();
            ToggleTheme.Checked = Config.ConfigTheme == 2;
        }

        #region "Config Click Events":
        private bool colorDialogOpen = false;
        private void FG1ColourText_Click(object sender, EventArgs e) {
            if(colorDialogOpen) { return; }
            colorDialogOpen = true;
            MetroColorDialog.Result res = new MetroColorDialog().Show(this, MetroColorDialog.HexStringToColor(FG1ColourText.Text));
            if(res.Status == MetroColorDialog.Status.Selected) {
                FG1ColourText.Text = MetroColorDialog.ColorToHexString(res.Color);
            }
            colorDialogOpen = false;
            FocusMe();
        }
        private void FG2ColourText_Click(object sender, EventArgs e) {
            if(colorDialogOpen) { return; }
            colorDialogOpen = true;
            MetroColorDialog.Result res = new MetroColorDialog().Show(this, MetroColorDialog.HexStringToColor(FG2ColourText.Text));
            if(res.Status == MetroColorDialog.Status.Selected) {
                FG2ColourText.Text = MetroColorDialog.ColorToHexString(res.Color);
            }
            colorDialogOpen = false;
            FocusMe();
        }
        private void BGColourText_Click(object sender, EventArgs e) {
            if(colorDialogOpen) { return; }
            colorDialogOpen = true;
            MetroColorDialog.Result res = new MetroColorDialog().Show(this, MetroColorDialog.HexStringToColor(BGColourText.Text));
            if(res.Status == MetroColorDialog.Status.Selected) {
                BGColourText.Text = MetroColorDialog.ColorToHexString(res.Color);
            }
            colorDialogOpen = false;
            FocusMe();
        }
        private void SelColourText_Click(object sender, EventArgs e) {
            if(colorDialogOpen) { return; }
            colorDialogOpen = true;
            MetroColorDialog.Result res = new MetroColorDialog().Show(this, MetroColorDialog.HexStringToColor(SelColourText.Text));
            if(res.Status == MetroColorDialog.Status.Selected) {
                SelColourText.Text = MetroColorDialog.ColorToHexString(res.Color);
            }
            colorDialogOpen = false;
            FocusMe();
        }
        private void AutoButton_Click(object sender, EventArgs e) {
            User32.GetClientRect(OverlayWindow.GenshinHandle, out User32.RECT rect);
            AssumeDefaultValues(rect);
            UpdateControlValues();
        }
        private void SaveButton_Click(object sender, EventArgs e) {
            ConfigPanel.Visible = false;
            ConfigureOverlayButton.Visible = true;
            Config.Save();
            OverlayWindow.IsConfiguring = false;
        }

        private void ToggleTheme_CheckedChanged(object sender, EventArgs e) {
            if(!ToggleTheme.Checked) {
                Config.ConfigTheme = 1;
                Theme = MetroThemeStyle.Light;
                MStyleManager.Theme = MetroThemeStyle.Light;
            } else {
                Config.ConfigTheme = 2;
                Theme = MetroThemeStyle.Dark;
                MStyleManager.Theme = MetroThemeStyle.Dark;
            }
        }

        private void DevLink_Click(object sender, EventArgs e) {
            Process.Start("https://streamlabs.com/primpri/tip");
        }
        #endregion //Click

        #region "Config ValueChanged/TextChanged Events":
        private void CooldownTextXPosTrack_ValueChanged(object sender, EventArgs e) {
            CooldownTextXPosText.Text = "X Pos: " + CooldownTextXPosTrack.Value.ToString();
            Config.CooldownTextLocation = new Point(CooldownTextXPosTrack.Value, Config.CooldownTextLocation.Y);
        }
        private void CooldownTextYPosTrack_ValueChanged(object sender, EventArgs e) {
            CooldownTextYPosText.Text = "Y Pos: " + CooldownTextYPosTrack.Value.ToString();
            Config.CooldownTextLocation = new Point(Config.CooldownTextLocation.X, CooldownTextYPosTrack.Value);
        }
        private void CooldownTextWidthTrack_ValueChanged(object sender, EventArgs e) {
            CooldownTextWidthText.Text = "Width: " + CooldownTextWidthTrack.Value.ToString();
            Config.CooldownTextSize = new Size(CooldownTextWidthTrack.Value, Config.CooldownTextSize.Height);
        }
        private void CooldownTextHeightTrack_ValueChanged(object sender, EventArgs e) {
            CooldownTextHeightText.Text = "Height: " + CooldownTextHeightTrack.Value.ToString();
            Config.CooldownTextSize = new Size(Config.CooldownTextSize.Width, CooldownTextHeightTrack.Value);
        }
        private void PartyNumXPosTrack_ValueChanged(object sender, EventArgs e) {
            PartyNumXPosText.Text = "X Pos: " + PartyNumXPosTrack.Value.ToString();
            Config.PartyNumLocation = new Point(PartyNumXPosTrack.Value, Config.PartyNumLocation.Y);
        }
        private void PartyNumYPosTrack_ValueChanged(object sender, EventArgs e) {
            PartyNumYPosText.Text = "Y Pos: " + PartyNumYPosTrack.Value.ToString();
            Config.PartyNumLocation = new Point(Config.PartyNumLocation.X, PartyNumYPosTrack.Value);
        }
        private void PartyNumYOffsetTrack_ValueChanged(object sender, EventArgs e) {
            PartyNumYOffsetText.Text = "Y Offset: " + PartyNumYOffsetTrack.Value.ToString();
            Config.PartyNumYOffset = PartyNumYOffsetTrack.Value;
        }
        private void CooldownBarsXPosTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsXPosText.Text = "X Pos: " + CooldownBarsXPosTrack.Value.ToString();
            Config.CooldownBarLocation = new Point(CooldownBarsXPosTrack.Value, Config.CooldownBarLocation.Y);
        }
        private void CooldownBarsYPosTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsYPosText.Text = "Y Pos: " + CooldownBarsYPosTrack.Value.ToString();
            Config.CooldownBarLocation = new Point(Config.CooldownBarLocation.X, CooldownBarsYPosTrack.Value);
        }
        private void CooldownBarsWidthTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsWidthText.Text = "Width: " + CooldownBarsWidthTrack.Value.ToString();
            Config.CooldownBarSize = new Size(CooldownBarsWidthTrack.Value, Config.CooldownBarSize.Height);
        }
        private void CooldownBarsHeightTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsHeightText.Text = "Height: " + CooldownBarsHeightTrack.Value.ToString();
            Config.CooldownBarSize = new Size(Config.CooldownBarSize.Width, CooldownBarsHeightTrack.Value);
        }
        private void CooldownBarsXOffsetTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsXOffsetText.Text = "X Offset: " + ((float)CooldownBarsXOffsetTrack.Value / 10).ToString();
            Config.CooldownBarOffset = new PointF((float)CooldownBarsXOffsetTrack.Value / 10, Config.CooldownBarOffset.Y);
        }
        private void CooldownBarsYOffsetTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsYOffsetText.Text = "Y Offset: " + ((float)CooldownBarsYOffsetTrack.Value / 10).ToString();
            Config.CooldownBarOffset = new PointF(Config.CooldownBarOffset.X, (float)CooldownBarsYOffsetTrack.Value / 10);
        }
        private void CooldownBarsRadiusTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsModeText.Text = "Style: " + CooldownBarsModeTrack.Value.ToString();
            Config.CooldownBarMode = CooldownBarsModeTrack.Value;
        }
        private void CooldownBarsSelOffsetTrack_ValueChanged(object sender, EventArgs e) {
            CooldownBarsSelOffsetText.Text = "Sel. Offset: " + CooldownBarsSelOffsetTrack.Value.ToString();
            Config.CooldownBarSelOffset = CooldownBarsSelOffsetTrack.Value;
        }

        private void CooldownPropMaxTrack_ValueChanged(object sender, EventArgs e) {
            CooldownPropMaxText.Text = "Max: " + CooldownPropMaxTrack.Value.ToString();
            Config.CooldownMaxPossible = CooldownPropMaxTrack.Value;
        }
        private void CooldownPropOffsetTrack_ValueChanged(object sender, EventArgs e) {
            CooldownPropOffsetText.Text = "Offset: " + ((decimal)CooldownPropOffsetTrack.Value / 10).ToString();
            Config.CooldownOffset = (decimal)CooldownPropOffsetTrack.Value / 10;
        }
        private void CooldownPropReapplyTrack_ValueChanged(object sender, EventArgs e) {
            CooldownPropReapplyText.Text = "Reapply: " + ((decimal)CooldownPropReapplyTrack.Value / 10).ToString();
            Config.CooldownMinimumReapply = (decimal)CooldownPropReapplyTrack.Value / 10;
        }
        private void CooldownPropOverrideTrack_ValueChanged(object sender, EventArgs e) {
            CooldownPropOverrideText.Text = "Override: " + ((decimal)CooldownPropOverrideTrack.Value / 10).ToString();
            Config.CooldownMinimumOverride = (decimal)CooldownPropOverrideTrack.Value / 10;
        }
        private void CooldownPropPauseTrack_ValueChanged(object sender, EventArgs e) {
            CooldownPropPauseText.Text = "Pause Sub: " + ((decimal)CooldownPropPauseTrack.Value / 10).ToString();
            Config.CooldownPauseSubtraction = (decimal)CooldownPropPauseTrack.Value / 10;
        }
        private void CooldownPropTickTrack_ValueChanged(object sender, EventArgs e) {
            CooldownPropTickText.Text = "Tick Rate: " + CooldownPropTickTrack.Value.ToString();
            Config.CooldownTickRateInMs = CooldownPropTickTrack.Value;
        }
        private void CooldownPropConfTrack_ValueChanged(object sender, EventArgs e) {
            CooldownPropConfText.Text = $"Confidence: {CooldownPropConfTrack.Value}%";
            Config.OCRMinimumConfidence = (float)CooldownPropConfTrack.Value / 100;
        }

        private void FG1ColourText_TextChanged(object sender, EventArgs e) {
            Config.CooldownBarFG1Color = FG1ColourText.Text;
            OverlayWindow.UpdateBrushes();
        }
        private void FG2ColourText_TextChanged(object sender, EventArgs e) {
            Config.CooldownBarFG2Color = FG2ColourText.Text;
            OverlayWindow.UpdateBrushes();
        }
        private void BGColourText_TextChanged(object sender, EventArgs e) {
            Config.CooldownBarBGColor = BGColourText.Text;
            OverlayWindow.UpdateBrushes();
        }
        private void SelColourText_TextChanged(object sender, EventArgs e) {
            Config.CooldownBarSelectedFGColor = SelColourText.Text;
            OverlayWindow.UpdateBrushes();
        }
        private void CooldownOverride1Text_TextChanged(object sender, EventArgs e) {
            if(int.TryParse(CooldownOverride1Text.Text, out int cd)) {
                Config.CooldownOverride[0] = cd;
            } else {
                Config.CooldownOverride[0] = 0;
            }
        }
        private void CooldownOverride2Text_TextChanged(object sender, EventArgs e) {
            if(int.TryParse(CooldownOverride2Text.Text, out int cd)) {
                Config.CooldownOverride[1] = cd;
            } else {
                Config.CooldownOverride[1] = 0;
            }
        }
        private void CooldownOverride3Text_TextChanged(object sender, EventArgs e) {
            if(int.TryParse(CooldownOverride3Text.Text, out int cd)) {
                Config.CooldownOverride[2] = cd;
            } else {
                Config.CooldownOverride[2] = 0;
            }
        }
        private void CooldownOverride4Text_TextChanged(object sender, EventArgs e) {
            if(int.TryParse(CooldownOverride4Text.Text, out int cd)) {
                Config.CooldownOverride[3] = cd;
            } else {
                Config.CooldownOverride[3] = 0;
            }
        }
        #endregion //ValueChanged/TextChanged

        #endregion //Config
    }
}
