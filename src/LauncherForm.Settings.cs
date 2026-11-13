using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal partial class LauncherForm
{
	private SmoothPanel pnlSetGfx;
	private SmoothPanel pnlSetGame;
	private SmoothPanel pnlSetHealth;
	private RoundedBtn[] setThemeBtns;
	private RoundedBtn btnSetBrowse;
	private RoundedBtn btnSetOpenFolder;
	private RoundedBtn btnSetModFolder;
	private RoundedBtn btnSetBackup;
	private RoundedBtn btnSetRestore;
	private RoundedBtn btnSetFixCrash;
	private RoundedBtn btnHealthRecheck;
	private Label lblSetModsBanner;
	private Label lblSetSound;
	private RoundedBtn btnSetTestSound;
	private Label lblSetDiscord;
	private Label lblSetSlot;
	private Label lblSetAltTab;
	private Label lblSetClose;
	private ToolTip setToolTip;

	private void BuildSetPage()
	{
		setToolTip = new ToolTip();
		// 1. Left Card: Graphics, FPS & Theme
		pnlSetGfx = new SmoothPanel(colCard, 10);
		pnlSetGfx.Location = new Point(0, 0);
		pnlSetGfx.Size = new Size(326, 258);
		pnlSetGfx.BorderColor = Color.FromArgb(40, 60, 95);
		pnlSet.Controls.Add(pnlSetGfx);

		AddLbl(pnlSetGfx, "🎮  ภาพและกราฟิก (GRAPHICS & FPS)", fSub, 14, 10, colAccent);

		AddLbl(pnlSetGfx, "โทนภาพ SkyGfx:", fSmall, 14, 34, colSub);
		btnSgPs2 = MakeMiniBtn("PS2 Classic", 14, 52, 144, 28);
		btnSgPs2.Click += delegate { ApplySkyGfxPreset("ps2"); };
		pnlSetGfx.Controls.Add(btnSgPs2);

		btnSgPc = MakeMiniBtn("PC Original", 166, 52, 146, 28);
		btnSgPc.Click += delegate { ApplySkyGfxPreset("pc"); };
		pnlSetGfx.Controls.Add(btnSgPc);

		btnSgMob = MakeMiniBtn("Mobile", 14, 84, 144, 28);
		btnSgMob.Click += delegate { ApplySkyGfxPreset("mobile"); };
		pnlSetGfx.Controls.Add(btnSgMob);

		btnSgOff = MakeMiniBtn("ปิดใช้งาน", 166, 84, 146, 28);
		btnSgOff.Click += delegate { ApplySkyGfxPreset("off"); };
		pnlSetGfx.Controls.Add(btnSgOff);

		AddLbl(pnlSetGfx, "จำกัดเฟรมเรต (FPS Limit):", fSmall, 14, 118, colSub);
		btnFps60 = MakeMiniBtn("60", 14, 138, 54, 28);
		btnFps60.Click += delegate { ApplyFpsLimit(60); };
		pnlSetGfx.Controls.Add(btnFps60);

		btnFps90 = MakeMiniBtn("90", 74, 138, 54, 28);
		btnFps90.Click += delegate { ApplyFpsLimit(90); };
		pnlSetGfx.Controls.Add(btnFps90);

		btnFps120 = MakeMiniBtn("120", 134, 138, 54, 28);
		btnFps120.Click += delegate { ApplyFpsLimit(120); };
		pnlSetGfx.Controls.Add(btnFps120);

		btnFps144 = MakeMiniBtn("144", 194, 138, 54, 28);
		btnFps144.Click += delegate { ApplyFpsLimit(144); };
		pnlSetGfx.Controls.Add(btnFps144);

		btnFpsMax = MakeMiniBtn("Max", 254, 138, 58, 28);
		btnFpsMax.Click += delegate { ApplyFpsLimit(0); };
		pnlSetGfx.Controls.Add(btnFpsMax);

		AddLbl(pnlSetGfx, "สีธีมหลัก (THEME ACCENT):", fSmall, 14, 174, colSub);
		Color[] array = new Color[6]
		{
			Color.FromArgb(51, 204, 255),
			Color.FromArgb(255, 235, 0),
			Color.FromArgb(102, 255, 0),
			Color.FromArgb(255, 0, 128),
			Color.FromArgb(138, 43, 226),
			Color.FromArgb(0, 230, 255)
		};
		setThemeBtns = new RoundedBtn[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			Color c = array[i];
			RoundedBtn roundedBtn = new RoundedBtn("", c, ControlPaint.Light(c, 0.2f), c, 6, fSmall);
			roundedBtn.Location = new Point(14 + i * 50, 196);
			roundedBtn.Size = new Size(42, 26);
			roundedBtn.Tag = c;
			roundedBtn.Click += delegate
			{
				ApplyAccent(c);
				SaveConfig();
				ToastOk("เปลี่ยนสีธีมแล้ว");
			};
			pnlSetGfx.Controls.Add(roundedBtn);
			setThemeBtns[i] = roundedBtn;
		}

		// 2. Right Card: Game Path & Mods
		pnlSetGame = new SmoothPanel(colCard, 10);
		pnlSetGame.Location = new Point(342, 0);
		pnlSetGame.Size = new Size(326, 258);
		pnlSetGame.BorderColor = Color.FromArgb(40, 60, 95);
		pnlSet.Controls.Add(pnlSetGame);

		AddLbl(pnlSetGame, "📁  ไฟล์เกมและม็อด (GAME & MODS)", fSub, 14, 10, colAccent);

		AddLbl(pnlSetGame, "โฟลเดอร์ติดตั้ง GTA San Andreas:", fSmall, 14, 34, colSub);
		txtPath = AddField(pnlSetGame, "", 14, 52, 214);
		txtPath.TextChanged += delegate
		{
			if (setToolTip != null && !txtPath.IsDisposed)
			{
				string p = FieldVal(txtPath);
				setToolTip.SetToolTip(txtPath, p);
				if (txtPath.Parent != null)
				{
					setToolTip.SetToolTip(txtPath.Parent, p);
				}
			}
		};
		btnSetBrowse = MakeMiniBtn("เลือก...", 234, 52, 78, 42);
		btnSetBrowse.BorderColor = colAccent;
		btnSetBrowse.Click += delegate
		{
			BrowseGameDir();
		};
		pnlSetGame.Controls.Add(btnSetBrowse);

		AddLbl(pnlSetGame, "ทางลัดเปิดโฟลเดอร์:", fSmall, 14, 106, colSub);
		btnSetOpenFolder = MakeMiniBtn("📂  โฟลเดอร์เกม", 14, 126, 144, 32);
		btnSetOpenFolder.BorderColor = Color.FromArgb(40, 60, 95);
		btnSetOpenFolder.Click += delegate
		{
			try
			{
				Process.Start("explorer.exe", GetGameDir());
			}
			catch
			{
			}
		};
		pnlSetGame.Controls.Add(btnSetOpenFolder);

		btnSetModFolder = MakeMiniBtn("📦  ModLoader", 166, 126, 146, 32);
		btnSetModFolder.BorderColor = colAccent;
		btnSetModFolder.Click += delegate
		{
			try
			{
				string ml = Path.Combine(GetGameDir(), "modloader");
				if (!Directory.Exists(ml))
				{
					Directory.CreateDirectory(ml);
				}
				Process.Start("explorer.exe", ml);
			}
			catch
			{
			}
		};
		pnlSetGame.Controls.Add(btnSetModFolder);

		AddLbl(pnlSetGame, "การสำรองและกู้คืนไฟล์ม็อด:", fSmall, 14, 168, colSub);
		btnSetBackup = MakeMiniBtn("💾  สำรองม็อด", 14, 188, 144, 30);
		btnSetBackup.BorderColor = Color.FromArgb(40, 60, 95);
		btnSetBackup.Click += delegate
		{
			CreateModBackup();
		};
		pnlSetGame.Controls.Add(btnSetBackup);

		btnSetRestore = MakeMiniBtn("↺  คืนค่าม็อดเดิม", 166, 188, 146, 30);
		btnSetRestore.BorderColor = Color.FromArgb(40, 60, 95);
		btnSetRestore.Click += delegate
		{
			RestoreModBackup();
		};
		pnlSetGame.Controls.Add(btnSetRestore);

		btnSetFixCrash = MakeMiniBtn("🛠  แก้เกมเด้ง (รีเซ็ต gta_sa.set)", 14, 222, 298, 28);
		btnSetFixCrash.BorderColor = Color.FromArgb(243, 156, 18);
		btnSetFixCrash.Click += delegate
		{
			ResetGameSettings();
		};
		pnlSetGame.Controls.Add(btnSetFixCrash);

		// 3. Bottom Card: System Integrity & Health
		pnlSetHealth = new SmoothPanel(colCard, 10);
		pnlSetHealth.Location = new Point(0, 266);
		pnlSetHealth.Size = new Size(668, 168);
		pnlSetHealth.BorderColor = Color.FromArgb(40, 60, 95);
		pnlSet.Controls.Add(pnlSetHealth);

		AddLbl(pnlSetHealth, "🛡  ตรวจสอบระบบ & ตัวเลือกการทำงาน (SYSTEM HEALTH & OPTIONS)", fSub, 14, 8, colAccent);

		btnAutoFix = MakeMiniBtn("⚡ แก้ไขอัตโนมัติ", 392, 6, 130, 26);
		btnAutoFix.BorderColor = Color.FromArgb(40, 60, 95);
		btnAutoFix.Click += delegate
		{
			RunAutoFix();
		};
		pnlSetHealth.Controls.Add(btnAutoFix);

		btnHealthRecheck = MakeMiniBtn("↻  ตรวจอีกครั้ง", 530, 6, 124, 26);
		btnHealthRecheck.BorderColor = colAccent;
		btnHealthRecheck.Click += delegate
		{
			RunHealthCheck();
		};
		pnlSetHealth.Controls.Add(btnHealthRecheck);

		lblHealth = new Label[5];
		lblHealth[0] = AddLbl(pnlSetHealth, "กำลังตรวจสอบ gta_sa.exe...", fSmall, 14, 34, colSub);
		lblHealth[1] = AddLbl(pnlSetHealth, "กำลังตรวจสอบ samp.dll...", fSmall, 14, 52, colSub);
		lblHealth[2] = AddLbl(pnlSetHealth, "กำลังตรวจสอบ ModLoader & CLEO...", fSmall, 14, 70, colSub);
		lblHealth[3] = AddLbl(pnlSetHealth, "กำลังตรวจสอบ FramerateVigilante...", fSmall, 340, 34, colSub);
		lblHealth[4] = AddLbl(pnlSetHealth, "กำลังตรวจสอบ SkyGfx...", fSmall, 340, 52, colSub);
		lblEngine = AddLbl(pnlSetHealth, "Engine: " + core.EngineName, fSmall, 340, 70, colSub);

		lblSetModsBanner = AddLbl(pnlSetHealth, "ม็อดหลักที่ทำงาน: FramerateVigilante • ModLoader v0.3.10 • CLEO 4.4 • SkyGfx • SilentPatch • 2DFX", fSmall, 14, 88, colSub);

		swSoundFx = new ToggleSwitch();
		swSoundFx.Location = new Point(14, 108);
		swSoundFx.CheckedChanged += delegate
		{
			soundFxEnabled = swSoundFx.Checked;
			CyberAudio.Enabled = soundFxEnabled;
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlSetHealth.Controls.Add(swSoundFx);
		lblSetSound = AddLbl(pnlSetHealth, "🔊  เสียงไซเบอร์", fSmall, 66, 112, colSub);

		btnSetTestSound = MakeMiniBtn("▶ ฟังเสียง", 168, 108, 68, 22);
		btnSetTestSound.BorderColor = colAccent;
		btnSetTestSound.Click += delegate
		{
			CyberAudio.PlayAlert();
		};
		pnlSetHealth.Controls.Add(btnSetTestSound);

		swDiscordRpc = new ToggleSwitch();
		swDiscordRpc.Location = new Point(257, 108);
		swDiscordRpc.CheckedChanged += delegate
		{
			discordRpcEnabled = swDiscordRpc.Checked;
			DiscordRpc.Enabled = discordRpcEnabled;
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlSetHealth.Controls.Add(swDiscordRpc);
		lblSetDiscord = AddLbl(pnlSetHealth, "🎮  Discord Rich Presence", fSmall, 309, 112, colSub);

		swAutoSlot = new ToggleSwitch();
		swAutoSlot.Location = new Point(486, 108);
		swAutoSlot.CheckedChanged += delegate
		{
			autoJoinSlot = swAutoSlot.Checked;
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlSetHealth.Controls.Add(swAutoSlot);
		lblSetSlot = AddLbl(pnlSetHealth, "⚡  ออโต้จอยเมื่อห้องว่าง", fSmall, 538, 112, colSub);

		swNoPauseAltTab = new ToggleSwitch();
		swNoPauseAltTab.Location = new Point(14, 138);
		swNoPauseAltTab.CheckedChanged += delegate
		{
			if (cfgReady)
			{
				ApplyNoPauseAltTab(swNoPauseAltTab.Checked);
			}
		};
		pnlSetHealth.Controls.Add(swNoPauseAltTab);
		lblSetAltTab = AddLbl(pnlSetHealth, "🖥  ไม่ค้างตอนพับจอ (Alt+Tab)", fSmall, 66, 142, colSub);

		swClose = new ToggleSwitch();
		swClose.Location = new Point(257, 138);
		swClose.CheckedChanged += delegate
		{
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlSetHealth.Controls.Add(swClose);
		lblSetClose = AddLbl(pnlSetHealth, "🚪  ปิดเมื่อเข้าเกม", fSmall, 309, 142, colSub);

		UpdateSkyGfxButtons();
		UpdateFpsButtons();
	}

	private void LayoutSettingsPage(int pageW, int pageH)
	{
		if (pnlSetGfx == null || pnlSetGame == null || pnlSetHealth == null) return;
		int gap = DpiHelper.S(14);
		int topH = DpiHelper.S(256);
		int leftW = (pageW - gap) / 2;
		int rightW = pageW - leftW - gap;
		int pad = DpiHelper.S(14);

		// 1. Left Card: Graphics & FPS
		pnlSetGfx.Location = new Point(0, 0);
		pnlSetGfx.Size = new Size(leftW, topH);
		int gInner = leftW - pad * 2;
		int gHalf = (gInner - DpiHelper.S(8)) / 2;
		int rowH = DpiHelper.S(28);
		if (btnSgPs2 != null) { btnSgPs2.Location = new Point(pad, DpiHelper.S(52)); btnSgPs2.Size = new Size(gHalf, rowH); }
		if (btnSgPc != null) { btnSgPc.Location = new Point(pad + gHalf + DpiHelper.S(8), DpiHelper.S(52)); btnSgPc.Size = new Size(gInner - gHalf - DpiHelper.S(8), rowH); }
		if (btnSgMob != null) { btnSgMob.Location = new Point(pad, DpiHelper.S(84)); btnSgMob.Size = new Size(gHalf, rowH); }
		if (btnSgOff != null) { btnSgOff.Location = new Point(pad + gHalf + DpiHelper.S(8), DpiHelper.S(84)); btnSgOff.Size = new Size(gInner - gHalf - DpiHelper.S(8), rowH); }

		int fpsGap = DpiHelper.S(6);
		int fpsW = (gInner - fpsGap * 4) / 5;
		RoundedBtn[] fpsBtns = new RoundedBtn[] { btnFps60, btnFps90, btnFps120, btnFps144, btnFpsMax };
		for (int i = 0; i < fpsBtns.Length; i++)
		{
			if (fpsBtns[i] != null)
			{
				int w = (i == fpsBtns.Length - 1) ? (gInner - (fpsW + fpsGap) * i) : fpsW;
				fpsBtns[i].Location = new Point(pad + i * (fpsW + fpsGap), DpiHelper.S(138));
				fpsBtns[i].Size = new Size(w, rowH);
			}
		}

		if (setThemeBtns != null && setThemeBtns.Length > 0)
		{
			int swGap = DpiHelper.S(8);
			int swW = (gInner - swGap * (setThemeBtns.Length - 1)) / setThemeBtns.Length;
			for (int i = 0; i < setThemeBtns.Length; i++)
			{
				if (setThemeBtns[i] != null)
				{
					int w = (i == setThemeBtns.Length - 1) ? (gInner - (swW + swGap) * i) : swW;
					setThemeBtns[i].Location = new Point(pad + i * (swW + swGap), DpiHelper.S(198));
					setThemeBtns[i].Size = new Size(w, DpiHelper.S(26));
				}
			}
		}

		// 2. Right Card: Game Path & Mods
		pnlSetGame.Location = new Point(leftW + gap, 0);
		pnlSetGame.Size = new Size(rightW, topH);
		int rInner = rightW - pad * 2;
		int browseW = DpiHelper.S(78);
		if (txtPath != null && txtPath.Parent != null)
		{
			txtPath.Parent.Location = new Point(pad, DpiHelper.S(52));
			txtPath.Parent.Size = new Size(Math.Max(DpiHelper.S(120), rInner - browseW - DpiHelper.S(6)), DpiHelper.S(42));
		}
		if (btnSetBrowse != null)
		{
			btnSetBrowse.Location = new Point(rightW - pad - browseW, DpiHelper.S(52));
			btnSetBrowse.Size = new Size(browseW, DpiHelper.S(42));
		}
		int rHalf = (rInner - DpiHelper.S(8)) / 2;
		if (btnSetOpenFolder != null) { btnSetOpenFolder.Location = new Point(pad, DpiHelper.S(126)); btnSetOpenFolder.Size = new Size(rHalf, DpiHelper.S(32)); }
		if (btnSetModFolder != null) { btnSetModFolder.Location = new Point(pad + rHalf + DpiHelper.S(8), DpiHelper.S(126)); btnSetModFolder.Size = new Size(rInner - rHalf - DpiHelper.S(8), DpiHelper.S(32)); }
		if (btnSetBackup != null) { btnSetBackup.Location = new Point(pad, DpiHelper.S(186)); btnSetBackup.Size = new Size(rHalf, DpiHelper.S(30)); }
		if (btnSetRestore != null) { btnSetRestore.Location = new Point(pad + rHalf + DpiHelper.S(8), DpiHelper.S(186)); btnSetRestore.Size = new Size(rInner - rHalf - DpiHelper.S(8), DpiHelper.S(30)); }
		if (btnSetFixCrash != null) { btnSetFixCrash.Location = new Point(pad, DpiHelper.S(220)); btnSetFixCrash.Size = new Size(rInner, DpiHelper.S(28)); }

		// 3. Bottom Card: System Health & Options
		int healthY = topH + DpiHelper.S(10);
		int healthH = Math.Max(DpiHelper.S(172), pageH - healthY);
		pnlSetHealth.Location = new Point(0, healthY);
		pnlSetHealth.Size = new Size(pageW, healthH);

		int reW = DpiHelper.S(124);
		int afW = DpiHelper.S(130);
		if (btnHealthRecheck != null)
		{
			btnHealthRecheck.Location = new Point(pageW - pad - reW, DpiHelper.S(6));
			btnHealthRecheck.Size = new Size(reW, DpiHelper.S(26));
		}
		if (btnAutoFix != null)
		{
			btnAutoFix.Location = new Point(pageW - pad - reW - DpiHelper.S(8) - afW, DpiHelper.S(6));
			btnAutoFix.Size = new Size(afW, DpiHelper.S(26));
		}

		int col2X = Math.Max(DpiHelper.S(340), pageW / 2);
		if (lblHealth != null && lblHealth.Length >= 5)
		{
			lblHealth[0].Location = new Point(pad, DpiHelper.S(34));
			lblHealth[1].Location = new Point(pad, DpiHelper.S(52));
			lblHealth[2].Location = new Point(pad, DpiHelper.S(70));
			lblHealth[3].Location = new Point(col2X, DpiHelper.S(34));
			lblHealth[4].Location = new Point(col2X, DpiHelper.S(52));
		}
		if (lblEngine != null) lblEngine.Location = new Point(col2X, DpiHelper.S(70));
		if (lblSetModsBanner != null) lblSetModsBanner.Location = new Point(pad, DpiHelper.S(88));

		int row1Y = DpiHelper.S(108);
		int row2Y = DpiHelper.S(138);
		int swW2 = DpiHelper.S(44);
		int swH2 = DpiHelper.S(24);
		int lblOffX = DpiHelper.S(52);
		int lblOffY = DpiHelper.S(4);

		int colA = pad;
		int colB = Math.Max(DpiHelper.S(254), (int)(pageW * 0.37));
		int colC = Math.Max(DpiHelper.S(486), (int)(pageW * 0.69));

		if (swSoundFx != null) { swSoundFx.Location = new Point(colA, row1Y); swSoundFx.Size = new Size(swW2, swH2); }
		if (lblSetSound != null) lblSetSound.Location = new Point(colA + lblOffX, row1Y + lblOffY);
		if (btnSetTestSound != null)
		{
			int sndBtnX = Math.Min(colB - DpiHelper.S(76), colA + DpiHelper.S(154));
			btnSetTestSound.Location = new Point(sndBtnX, row1Y + DpiHelper.S(1));
			btnSetTestSound.Size = new Size(DpiHelper.S(68), DpiHelper.S(22));
		}
		if (swDiscordRpc != null) { swDiscordRpc.Location = new Point(colB, row1Y); swDiscordRpc.Size = new Size(swW2, swH2); }
		if (lblSetDiscord != null) lblSetDiscord.Location = new Point(colB + lblOffX, row1Y + lblOffY);
		if (swAutoSlot != null) { swAutoSlot.Location = new Point(colC, row1Y); swAutoSlot.Size = new Size(swW2, swH2); }
		if (lblSetSlot != null) lblSetSlot.Location = new Point(colC + lblOffX, row1Y + lblOffY);

		if (swNoPauseAltTab != null) { swNoPauseAltTab.Location = new Point(colA, row2Y); swNoPauseAltTab.Size = new Size(swW2, swH2); }
		if (lblSetAltTab != null) lblSetAltTab.Location = new Point(colA + lblOffX, row2Y + lblOffY);
		if (swClose != null) { swClose.Location = new Point(colB, row2Y); swClose.Size = new Size(swW2, swH2); }
		if (lblSetClose != null) lblSetClose.Location = new Point(colB + lblOffX, row2Y + lblOffY);
	}

	private void ApplySkyGfxPreset(string mode)
	{
		string g = GetGameDir();
		try
		{
			string msg = core.ApplySkyGfx(g, mode);
			activeSkyGfx = mode;
			UpdateSkyGfxButtons();
			SaveConfig();
			ToastOk(msg);
		}
		catch (Exception ex)
		{
			ShowErr("ไม่สามารถเปลี่ยน SkyGfx preset: " + ex.Message);
		}
	}

	private void UpdateSkyGfxButtons()
	{
		if (btnSgPs2 == null)
		{
			return;
		}
		Color cAct = colAccent;
		Color cActHi = colAccentHi;
		Color cNorm = colField;
		Color cNormHi = colFieldHover;

		Color activeBorder = Color.FromArgb(200, colAccentHi);
		Color normalBorder = Color.FromArgb(35, 52, 80);

		btnSgPs2.SetPalette((activeSkyGfx == "ps2") ? cAct : cNorm, (activeSkyGfx == "ps2") ? cActHi : cNormHi);
		btnSgPs2.TextColor = (activeSkyGfx == "ps2") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgPs2.BorderColor = (activeSkyGfx == "ps2") ? activeBorder : normalBorder;

		btnSgPc.SetPalette((activeSkyGfx == "pc") ? cAct : cNorm, (activeSkyGfx == "pc") ? cActHi : cNormHi);
		btnSgPc.TextColor = (activeSkyGfx == "pc") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgPc.BorderColor = (activeSkyGfx == "pc") ? activeBorder : normalBorder;

		btnSgMob.SetPalette((activeSkyGfx == "mobile") ? cAct : cNorm, (activeSkyGfx == "mobile") ? cActHi : cNormHi);
		btnSgMob.TextColor = (activeSkyGfx == "mobile") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgMob.BorderColor = (activeSkyGfx == "mobile") ? activeBorder : normalBorder;

		btnSgOff.SetPalette((activeSkyGfx == "off") ? cAct : cNorm, (activeSkyGfx == "off") ? cActHi : cNormHi);
		btnSgOff.TextColor = (activeSkyGfx == "off") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgOff.BorderColor = (activeSkyGfx == "off") ? activeBorder : normalBorder;
	}

	private void ApplyFpsLimit(int fps)
	{
		string gameDir = GetGameDir();
		try
		{
			string msg = core.ApplyFpsLimit(gameDir, fps);
			activeFpsLimit = fps;
			UpdateFpsButtons();
			SaveConfig();
			ToastOk(msg);
		}
		catch (Exception ex)
		{
			ShowErr("ไม่สามารถตั้งค่า FPS: " + ex.Message);
		}
	}

	private void UpdateFpsButtons()
	{
		if (btnFps60 == null)
		{
			return;
		}
		Color cAct = colYellow;
		Color cActHi = colYellowHi;
		Color cNorm = colField;
		Color cNormHi = colFieldHover;

		RoundedBtn[] btns = new RoundedBtn[5] { btnFps60, btnFps90, btnFps120, btnFps144, btnFpsMax };
		int[] vals = new int[5] { 60, 90, 120, 144, 0 };

		for (int i = 0; i < btns.Length; i++)
		{
			bool active = (activeFpsLimit == vals[i]);
			btns[i].SetPalette(active ? cAct : cNorm, active ? cActHi : cNormHi);
			btns[i].TextColor = active ? Color.FromArgb(11, 14, 20) : colSub;
			btns[i].BorderColor = active ? Color.FromArgb(255, 245, 120) : Color.FromArgb(35, 52, 80);
		}
	}

	private void RestoreModBackup()
	{
		string g = GetGameDir();
		List<SnapshotInfo> snaps = new List<SnapshotInfo>();
		try
		{
			snaps = core.ListSnapshots(g);
		}
		catch
		{
		}
		if (snaps.Count > 0)
		{
			ShowSnapshotManager(g, snaps);
			return;
		}
		string b = Path.Combine(g, "_mod_backup");
		if (!Directory.Exists(b))
		{
			ShowErr("ไม่พบ Snapshot หรือ _mod_backup ในโฟลเดอร์เกม");
			return;
		}
		if (MessageBox.Show(this, "คุณต้องการคืนค่าไฟล์จาก _mod_backup กลับไปยังโฟลเดอร์เกมหรือไม่?\r\n(ไฟล์คอนฟิกเดิมจะถูกนำกลับมาใช้งาน)", "ยืนยันการคืนค่าม็อด", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			foreach (string file in Directory.GetFiles(b, "*.*", SearchOption.AllDirectories))
			{
				string rel = file.Substring(b.Length).TrimStart('\\', '/');
				if (rel.EndsWith(".bak"))
				{
					rel = rel.Substring(0, rel.Length - 4);
				}
				string dest = Path.Combine(g, rel);
				string dir = Path.GetDirectoryName(dest);
				if (!Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}
				File.Copy(file, dest, true);
			}
			ToastOk("คืนค่าม็อดเรียบร้อยแล้ว");
			RunHealthCheck();
		}
		catch (Exception ex)
		{
			ShowErr("เกิดข้อผิดพลาดในการคืนค่าม็อด:\r\n" + ex.Message);
		}
	}

	private void CreateModBackup()
	{
		string g = GetGameDir();
		string note = AskSnapshotName("Snapshot " + DateTime.Now.ToString("dd/MM HH:mm"));
		if (note == null)
		{
			return;
		}
		SetStatus("กำลังบีบอัด Snapshot ม็อด (.ini/.asi/.cleo/modloader)...");
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				string summary = core.CreateSnapshot(g, note);
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						SetStatus(summary.Replace("\r\n", " • "));
						if (summary.IndexOf("⚠") >= 0)
						{
							MessageBox.Show(this, summary, "สำรอง Snapshot แล้ว (มีคำเตือน)", MessageBoxButtons.OK, MessageBoxIcon.Warning);
						}
						else
						{
							ToastOk(summary);
						}
					});
				}
			}
			catch (Exception ex)
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						ShowErr("การสำรองข้อมูลล้มเหลว:\r\n" + ex.Message);
					});
				}
			}
		});
	}

	private string AskSnapshotName(string defName)
	{
		using (Form dlg = new Form())
		{
			dlg.Text = "สร้าง Snapshot สำรองม็อด";
			dlg.ClientSize = new Size(360, 128);
			dlg.StartPosition = FormStartPosition.CenterParent;
			dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
			dlg.MaximizeBox = false;
			dlg.MinimizeBox = false;
			dlg.BackColor = colBg;
			dlg.ForeColor = colText;

			Panel pWrap = new Panel();
			pWrap.Dock = DockStyle.Fill;
			pWrap.BackColor = colBg;
			dlg.Controls.Add(pWrap);

			Label lbl = AddLbl(pWrap, "ชื่อ Snapshot (สำรอง .ini / .asi / .cleo / modloader):", fSmall, 14, 14, colSub);
			TextBox tb = AddField(pWrap, defName, 14, 36, 332);
			ForceText(tb, defName);
			tb.SelectAll();

			RoundedBtn ok = MakeMiniBtn("💾 สร้าง Snapshot", 174, 82, 172, 30);
			ok.BorderColor = colOk;
			RoundedBtn cancel = MakeMiniBtn("ยกเลิก", 14, 82, 92, 30);
			cancel.BorderColor = colField;

			string result = null;
			ok.Click += delegate { result = tb.Text.Trim(); dlg.DialogResult = DialogResult.OK; dlg.Close(); };
			cancel.Click += delegate { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
			dlg.AcceptButton = ok;
			dlg.CancelButton = cancel;
			pWrap.Controls.Add(ok);
			pWrap.Controls.Add(cancel);
			return (dlg.ShowDialog(this) == DialogResult.OK) ? result : null;
		}
	}

	private void ShowSnapshotManager(string gameDir, List<SnapshotInfo> snaps)
	{
		using (Form dlg = new Form())
		{
			dlg.Text = "จัดการ Snapshot ม็อด (Zstd Deduplicated)";
			dlg.ClientSize = new Size(520, 310);
			dlg.StartPosition = FormStartPosition.CenterParent;
			dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
			dlg.MaximizeBox = false;
			dlg.MinimizeBox = false;
			dlg.BackColor = colBg;
			dlg.ForeColor = colText;

			ListBox lb = new ListBox();
			lb.Location = new Point(14, 14);
			lb.Size = new Size(492, 238);
			lb.BackColor = colCard;
			lb.ForeColor = colText;
			lb.BorderStyle = BorderStyle.FixedSingle;
			lb.Font = fSmall;
			dlg.Controls.Add(lb);

			MethodInvoker reload = delegate
			{
				lb.Items.Clear();
				snaps = core.ListSnapshots(gameDir);
				foreach (SnapshotInfo s in snaps)
				{
					long rawKb = Math.Max(1, s.TotalRawBytes / 1024);
					long compKb = Math.Max(1, s.TotalCompressedBytes / 1024);
					string warn = (s.SkippedLargeCount > 0) ? ("  [⚠ ข้าม >32MB: " + s.SkippedLargeCount + "]") : "";
					lb.Items.Add(s.Name + "  [" + s.CreatedAt + "]  (" + s.FileCount + " ไฟล์, " + rawKb + " KB → " + compKb + " KB)" + warn);
				}
				if (lb.Items.Count > 0) lb.SelectedIndex = 0;
			};
			reload();

			RoundedBtn btnRest = MakeMiniBtn("↺ กู้คืน Snapshot ที่เลือก", 316, 264, 190, 32);
			btnRest.BorderColor = colOk;
			btnRest.Click += delegate
			{
				int idx = lb.SelectedIndex;
				if (idx < 0 || idx >= snaps.Count) return;
				SnapshotInfo sel = snaps[idx];
				if (MessageBox.Show(dlg, "กู้คืน Snapshot '" + sel.Name + "' (" + sel.FileCount + " ไฟล์) กลับไปยังโฟลเดอร์เกม?\r\n(ระบบจะสร้าง Auto Pre-Restore สำรองสถานะปัจจุบันไว้ให้ก่อนอัตโนมัติ)", "ยืนยันการกู้คืน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
				try
				{
					string msg = core.RestoreSnapshot(gameDir, sel.Id);
					reload();
					RunHealthCheck();
					MessageBox.Show(dlg, msg, "กู้คืน Snapshot สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
					dlg.Close();
				}
				catch (Exception ex)
				{
					ShowErr(ex.Message);
				}
			};
			dlg.Controls.Add(btnRest);

			RoundedBtn btnDel = MakeMiniBtn("🗑️ ลบ", 14, 264, 90, 32);
			btnDel.BorderColor = Color.FromArgb(255, 99, 99);
			btnDel.Click += delegate
			{
				int idx = lb.SelectedIndex;
				if (idx < 0 || idx >= snaps.Count) return;
				SnapshotInfo sel = snaps[idx];
				try
				{
					core.DeleteSnapshot(gameDir, sel.Id);
					reload();
				}
				catch (Exception ex)
				{
					ShowErr(ex.Message);
				}
			};
			dlg.Controls.Add(btnDel);

			RoundedBtn btnClose = MakeMiniBtn("ปิด", 216, 264, 90, 32);
			btnClose.BorderColor = colField;
			btnClose.Click += delegate { dlg.Close(); };
			dlg.Controls.Add(btnClose);

			dlg.ShowDialog(this);
		}
	}

	private static Color HealthColor(string status, Color cOk, Color cWarn, Color cBad, Color cSub)
	{
		if (status == "ok") return cOk;
		if (status == "warn") return cWarn;
		if (status == "bad") return cBad;
		return cSub;
	}

	private void RunAutoFix()
	{
		string g = GetGameDir();
		HealthCheckReport rep = lastHealthReport ?? core.CheckHealth(g, activeSkyGfx);
		lastHealthReport = rep;
		if (rep == null || rep.FixableCount == 0)
		{
			if (rep != null && (rep.InProgramFiles || rep.MissingDx9))
			{
				ShowErr("ปัญหาที่พบ (Program Files หรือ DirectX 9) ต้องแก้ไขด้วยตนเอง ไม่สามารถ Auto-Fix ได้");
			}
			else
			{
				ToastOk("ระบบปกติดี ไม่มีปัญหาที่ต้องซ่อมอัตโนมัติ");
			}
			return;
		}
		StringBuilder q = new StringBuilder();
		q.AppendLine("ต้องการดำเนินการซ่อมอัตโนมัติรายการต่อไปนี้หรือไม่?");
		if (rep.DuplicateAsis.Count > 0)
		{
			q.AppendLine("• ปิดไฟล์ .asi ที่ซ้ำซ้อน (.disabled): " + string.Join(", ", rep.DuplicateAsis.ToArray()));
		}
		if (rep.ReadonlyFiles.Count > 0)
		{
			q.AppendLine("• ปลดสถานะ Read-Only: " + string.Join(", ", rep.ReadonlyFiles.ToArray()));
		}
		if (MessageBox.Show(this, q.ToString(), "แก้ไขอัตโนมัติ (Auto-fix)", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		string msg = core.AutoFixHealth(g);
		MessageBox.Show(this, msg, "ผลการตรวจซ่อมระบบ", MessageBoxButtons.OK, MessageBoxIcon.Information);
		RunHealthCheck();
	}

	private void SetHealth(int i, string s, Color c)
	{
		if (lblHealth != null && i >= 0 && i < lblHealth.Length && lblHealth[i] != null && !lblHealth[i].IsDisposed)
		{
			lblHealth[i].UseMnemonic = false;
			lblHealth[i].Text = (s ?? "").Replace("\uFE0F", "");
			lblHealth[i].ForeColor = c;
		}
	}

	private void RunHealthCheck()
	{
		if (lblHealth == null)
		{
			return;
		}
		for (int i = 0; i < lblHealth.Length; i++)
		{
			SetHealth(i, "กำลังตรวจสอบ...", colSub);
		}
		Color cOk = colOk;
		Color cWarn = Color.FromArgb(243, 156, 18);
		Color cBad = Color.FromArgb(255, 99, 99);
		string g = GetGameDir();
		string preset = activeSkyGfx;
		ThreadPool.QueueUserWorkItem(delegate
		{
			HealthCheckReport rep = core.CheckHealth(g, preset);
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						lastHealthReport = rep;
						SetHealth(0, rep.GtaMsg, HealthColor(rep.GtaStatus, cOk, cWarn, cBad, colSub));
						SetHealth(1, rep.SampMsg, HealthColor(rep.SampStatus, cOk, cWarn, cBad, colSub));
						SetHealth(2, rep.ModsMsg, HealthColor(rep.ModsStatus, cOk, cWarn, cBad, colSub));
						SetHealth(3, rep.EnvMsg, HealthColor(rep.EnvStatus, cOk, cWarn, cBad, colSub));
						SetHealth(4, rep.GfxMsg, HealthColor(rep.GfxStatus, cOk, cWarn, cBad, colSub));
						if (btnAutoFix != null && !btnAutoFix.IsDisposed)
						{
							btnAutoFix.Enabled = rep.FixableCount > 0;
							if (rep.FixableCount > 0)
							{
								btnAutoFix.Text = "⚡ แก้ไข (" + rep.FixableCount + ")";
								btnAutoFix.BorderColor = cWarn;
							}
							else
							{
								btnAutoFix.Text = "⚡ แก้ไขอัตโนมัติ";
								btnAutoFix.BorderColor = Color.FromArgb(40, 60, 95);
							}
						}
					});
				}
			}
			catch
			{
			}
		});
	}

	private void ResetGameSettings()
	{
		string docPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		string setPath = Path.Combine(docPath, "GTA San Andreas User Files", "gta_sa.set");
		if (!File.Exists(setPath))
		{
			Toast("ไม่พบไฟล์ gta_sa.set (ยังไม่มีไฟล์คอนฟิกเดิม)");
			return;
		}
		if (MessageBox.Show(this, "คุณต้องการรีเซ็ตการตั้งค่าเกมหรือไม่?\r\n\r\nการทำงานนี้จะช่วยแก้ปัญหา:\r\n- เปิดเกมแล้วเด้ง / จอดำ / ค้างหน้าโหลด\r\n- ความละเอียดหน้าจอไม่ถูกต้อง\r\n\r\n(โปรแกรมจะสำรอง gta_sa.set.bak ไว้ และไฟล์เซฟจะไม่สูญหาย)", "ยืนยันการแก้ปัญหาเกมเด้ง", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			Process[] procs = Process.GetProcessesByName("gta_sa");
			foreach (Process p in procs)
			{
				try { p.Kill(); } catch { }
			}
			string bakPath = setPath + ".bak";
			File.Copy(setPath, bakPath, true);
			File.Delete(setPath);
			ToastOk("รีเซ็ต gta_sa.set และแก้ปัญหาเกมเด้งเรียบร้อยแล้ว");
		}
		catch (Exception ex)
		{
			ShowErr("ไม่สามารถรีเซ็ต gta_sa.set: " + ex.Message);
		}
	}

	private void ApplyNoPauseAltTab(bool enable)
	{
		noPauseAltTabEnabled = enable;
		string g = GetGameDir();
		string iniPath = Path.Combine(g, "scripts", "GTASA.WidescreenFix.ini");
		try
		{
			if (File.Exists(iniPath))
			{
				string[] lines = File.ReadAllLines(iniPath, Encoding.UTF8);
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].TrimStart().StartsWith("AllowAltTabbingWithoutPausing", StringComparison.OrdinalIgnoreCase))
					{
						lines[i] = "AllowAltTabbingWithoutPausing = " + (enable ? "1" : "0");
					}
				}
				File.WriteAllLines(iniPath, lines, Encoding.UTF8);
			}
			SaveConfig();
			if (enable)
			{
				ToastOk("เปิดใช้งาน: ไม่ค้าง/ไม่หลุดตอนพับจอ (Alt+Tab)");
			}
			else
			{
				Toast("ปิดใช้งาน: โหมดพับจอมาตรฐาน");
			}
		}
		catch (Exception ex)
		{
			ShowErr("ไม่สามารถบันทึกการตั้งค่า Alt+Tab: " + ex.Message);
		}
	}

	private void LoadConfig()
	{
		string text = "";
		string text2 = "";
		string text3 = "";
		string text4 = "";
		string text5 = "";
		string text6 = "";
		string text7 = "";
		bool @checked = false;
		bool checked2 = false;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		try
		{
			if (File.Exists(cfgPath))
			{
				string[] array = File.ReadAllLines(cfgPath, Encoding.UTF8);
				foreach (string text8 in array)
				{
					if (text8.StartsWith("nick=", StringComparison.OrdinalIgnoreCase))
					{
						text = text8.Substring(5);
					}
					else if (text8.StartsWith("accent=", StringComparison.OrdinalIgnoreCase))
					{
						text2 = text8.Substring(7);
					}
					else if (text8.StartsWith("gamepath=", StringComparison.OrdinalIgnoreCase))
					{
						text3 = text8.Substring(9);
					}
					else if (text8.StartsWith("autoc=", StringComparison.OrdinalIgnoreCase))
					{
						@checked = text8.Substring(6) == "1";
					}
					else if (text8.StartsWith("closeafter=", StringComparison.OrdinalIgnoreCase))
					{
						checked2 = text8.Substring(11) == "1";
					}
					else if (text8.StartsWith("last=", StringComparison.OrdinalIgnoreCase))
					{
						text4 = text8.Substring(5);
					}
					else if (text8.StartsWith("rconip=", StringComparison.OrdinalIgnoreCase))
					{
						text5 = text8.Substring(7);
					}
					else if (text8.StartsWith("rconport=", StringComparison.OrdinalIgnoreCase))
					{
						text6 = text8.Substring(9);
					}
					else if (text8.StartsWith("rconpass=", StringComparison.OrdinalIgnoreCase))
					{
						text7 = text8.Substring(9);
					}
					else if (text8.StartsWith("sortcol=", StringComparison.OrdinalIgnoreCase))
					{
						int result;
						if (int.TryParse(text8.Substring(8), out result) && result >= 0 && result <= 4)
						{
							sortCol = result;
						}
					}
					else if (text8.StartsWith("sortdesc=", StringComparison.OrdinalIgnoreCase))
					{
						sortDesc = text8.Substring(9) == "1";
					}
					else if (text8.StartsWith("srv=", StringComparison.OrdinalIgnoreCase))
					{
						list.Add(text8.Substring(4));
					}
					else if (text8.StartsWith("hist=", StringComparison.OrdinalIgnoreCase))
					{
						list2.Add(text8.Substring(5));
					}
					else if (text8.StartsWith("nick_prof=", StringComparison.OrdinalIgnoreCase))
					{
						string np = text8.Substring(10).Trim();
						if (np.Length > 0 && !nickProfiles.Contains(np))
						{
							nickProfiles.Add(np);
						}
					}
					else if (text8.StartsWith("skygfx_preset=", StringComparison.OrdinalIgnoreCase))
					{
						activeSkyGfx = text8.Substring(14).Trim();
					}
					else if (text8.StartsWith("fps_limit=", StringComparison.OrdinalIgnoreCase))
					{
						int parsedFps;
						if (int.TryParse(text8.Substring(10).Trim(), out parsedFps))
						{
							activeFpsLimit = parsedFps;
						}
					}
					else if (text8.StartsWith("autoslot=", StringComparison.OrdinalIgnoreCase))
					{
						autoJoinSlot = (text8.Substring(9).Trim() == "1");
					}
					else if (text8.StartsWith("sound_fx=", StringComparison.OrdinalIgnoreCase))
					{
						soundFxEnabled = (text8.Substring(9).Trim() == "1");
					}
					else if (text8.StartsWith("discord_rpc=", StringComparison.OrdinalIgnoreCase))
					{
						discordRpcEnabled = (text8.Substring(12).Trim() == "1");
					}
					else if (text8.StartsWith("discord_app_id=", StringComparison.OrdinalIgnoreCase))
					{
						string cid = text8.Substring(15).Trim();
						if (!string.IsNullOrEmpty(cid))
						{
							discordClientId = cid;
						}
					}
					else if (text8.StartsWith("nopause_alttab=", StringComparison.OrdinalIgnoreCase))
					{
						noPauseAltTabEnabled = (text8.Substring(15).Trim() == "1");
					}
				}
			}
		}
		catch
		{
		}
		if (text3.Length > 0)
		{
			ForceText(txtPath, text3);
		}
		else
		{
			ForceText(txtPath, AppDomain.CurrentDomain.BaseDirectory);
		}
		if (text5.Length > 0)
		{
			ForceText(txtRconIp, text5);
		}
		if (text6.Length > 0)
		{
			ForceText(txtRconPort, text6);
		}
		if (text7.Length > 0)
		{
			ForceText(txtRconPass, text7);
		}
		swAuto.Checked = @checked;
		swClose.Checked = checked2;
		if (swAutoSlot != null)
		{
			swAutoSlot.Checked = autoJoinSlot;
		}
		if (swSoundFx != null)
		{
			swSoundFx.Checked = soundFxEnabled;
		}
		CyberAudio.Enabled = soundFxEnabled;
		if (swDiscordRpc != null)
		{
			swDiscordRpc.Checked = discordRpcEnabled;
		}
		DiscordRpc.Enabled = discordRpcEnabled;
		DiscordRpc.ClientId = discordClientId;
		if (swNoPauseAltTab != null)
		{
			string gDir = (text3.Length > 0 && Directory.Exists(text3)) ? text3 : AppDomain.CurrentDomain.BaseDirectory;
			string wsIni = Path.Combine(gDir, "scripts", "GTASA.WidescreenFix.ini");
			if (File.Exists(wsIni))
			{
				try
				{
					string[] wsLines = File.ReadAllLines(wsIni, Encoding.UTF8);
					foreach (string wsL in wsLines)
					{
						if (wsL.TrimStart().StartsWith("AllowAltTabbingWithoutPausing", StringComparison.OrdinalIgnoreCase))
						{
							string[] parts = wsL.Split('=');
							if (parts.Length > 1)
							{
								noPauseAltTabEnabled = parts[1].Trim().StartsWith("1");
							}
							break;
						}
					}
				}
				catch { }
			}
			swNoPauseAltTab.Checked = noPauseAltTabEnabled;
		}
		UpdateSkyGfxButtons();
		UpdateFpsButtons();
		if (text2.Length > 0)
		{
			string[] array2 = text2.Split(',');
			if (array2.Length == 3)
			{
				try
				{
					ApplyAccent(Color.FromArgb(Clamp255(array2[0]), Clamp255(array2[1]), Clamp255(array2[2])));
				}
				catch
				{
				}
			}
		}
		foreach (string item in list)
		{
			string[] array3 = item.Split(new char[1] { '|' }, 3);
			if (array3.Length >= 2 && array3[0].Length > 0 && array3[1].Length > 0)
			{
				ServerEntry serverEntry = new ServerEntry();
				serverEntry.Ip = array3[0];
				serverEntry.Port = array3[1];
				serverEntry.Fav = true;
				if (array3.Length >= 3 && array3[2].Trim().Length > 0)
				{
					serverEntry.Name = array3[2].Trim();
				}
				favs.Add(serverEntry);
			}
		}
		foreach (string item2 in list2)
		{
			string[] array4 = item2.Split(new char[1] { '|' }, 3);
			if (array4.Length >= 2 && array4[0].Length > 0 && array4[1].Length > 0)
			{
				ServerEntry serverEntry2 = new ServerEntry();
				serverEntry2.Ip = array4[0];
				serverEntry2.Port = array4[1];
				serverEntry2.Fav = false;
				if (array4.Length >= 3 && array4[2].Trim().Length > 0)
				{
					serverEntry2.Name = array4[2].Trim();
				}
				hist.Add(serverEntry2);
			}
		}
		RebuildList();
		if (text.Length > 0)
		{
			ForceText(txtNick, text);
		}
		else
		{
			ForceText(txtNick, RandomNick());
		}
		if (text4.Length > 0)
		{
			string[] array5 = text4.Split('|');
			if (array5.Length >= 2)
			{
				ForceText(txtIp, array5[0]);
				ForceText(txtPort, array5[1]);
			}
		}
		else
		{
			ForceText(txtIp, "127.0.0.1");
			ForceText(txtPort, "7777");
		}
		SetStatus("โหลดการตั้งค่าเรียบร้อย");
		cfgReady = true;
		RebuildChips();
		LoadPlaytime();
		RenderBg();
	}

	private int Clamp255(string s)
	{
		int result;
		if (!int.TryParse(s.Trim(), out result))
		{
			result = 0;
		}
		if (result < 0)
		{
			result = 0;
		}
		if (result > 255)
		{
			result = 255;
		}
		return result;
	}

	private void SaveConfig()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			string text = FieldVal(txtNick);
			if (text.Length > 0)
			{
				stringBuilder.Append("nick=").Append(text).Append("\r\n");
			}
			stringBuilder.Append("accent=").Append(colAccent.R).Append(',')
				.Append(colAccent.G)
				.Append(',')
				.Append(colAccent.B)
				.Append("\r\n");
			string text2 = FieldVal(txtPath);
			if (text2.Length > 0)
			{
				stringBuilder.Append("gamepath=").Append(text2).Append("\r\n");
			}
			stringBuilder.Append("autoc=").Append(swAuto.Checked ? "1" : "0").Append("\r\n");
			stringBuilder.Append("closeafter=").Append(swClose.Checked ? "1" : "0").Append("\r\n");
			string text3 = FieldVal(txtIp);
			string text4 = FieldVal(txtPort);
			if (text3.Length > 0 && text4.Length > 0)
			{
				stringBuilder.Append("last=").Append(text3).Append('|')
					.Append(text4)
					.Append("\r\n");
			}
			string text5 = FieldVal(txtRconIp);
			string text6 = FieldVal(txtRconPort);
			string text7 = FieldVal(txtRconPass);
			if (text5.Length > 0)
			{
				stringBuilder.Append("rconip=").Append(text5).Append("\r\n");
			}
			if (text6.Length > 0)
			{
				stringBuilder.Append("rconport=").Append(text6).Append("\r\n");
			}
			if (text7.Length > 0)
			{
				stringBuilder.Append("rconpass=").Append(text7).Append("\r\n");
			}
			stringBuilder.Append("sortcol=").Append(sortCol).Append("\r\n");
			stringBuilder.Append("sortdesc=").Append(sortDesc ? "1" : "0").Append("\r\n");
			foreach (ServerEntry fav in favs)
			{
				stringBuilder.Append("srv=").Append(fav.Ip).Append('|').Append(fav.Port);
				string safeName = (fav.Name ?? "").Replace("|", " ").Replace("\r", "").Replace("\n", "").Trim();
				if (safeName.Length > 0)
				{
					stringBuilder.Append('|').Append(safeName);
				}
				stringBuilder.Append("\r\n");
			}
			foreach (ServerEntry item in hist)
			{
				stringBuilder.Append("hist=").Append(item.Ip).Append('|').Append(item.Port);
				string safeName = (item.Name ?? "").Replace("|", " ").Replace("\r", "").Replace("\n", "").Trim();
				if (safeName.Length > 0)
				{
					stringBuilder.Append('|').Append(safeName);
				}
				stringBuilder.Append("\r\n");
			}
			stringBuilder.Append("skygfx_preset=").Append(activeSkyGfx).Append("\r\n");
			stringBuilder.Append("fps_limit=").Append(activeFpsLimit).Append("\r\n");
			stringBuilder.Append("autoslot=").Append(autoJoinSlot ? "1" : "0").Append("\r\n");
			stringBuilder.Append("sound_fx=").Append(soundFxEnabled ? "1" : "0").Append("\r\n");
			stringBuilder.Append("discord_rpc=").Append(discordRpcEnabled ? "1" : "0").Append("\r\n");
			stringBuilder.Append("discord_app_id=").Append(discordClientId).Append("\r\n");
			stringBuilder.Append("nopause_alttab=").Append(noPauseAltTabEnabled ? "1" : "0").Append("\r\n");
			foreach (string prof in nickProfiles)
			{
				stringBuilder.Append("nick_prof=").Append(prof).Append("\r\n");
			}
			File.WriteAllText(cfgPath, stringBuilder.ToString(), Encoding.UTF8);
		}
		catch
		{
		}
	}

	private void BrowseGameDir()
	{
		using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
		{
			folderBrowserDialog.Description = "เลือกโฟลเดอร์ที่มี gta_sa.exe";
			if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
			{
				if (File.Exists(Path.Combine(folderBrowserDialog.SelectedPath, "gta_sa.exe")))
				{
					ForceText(txtPath, folderBrowserDialog.SelectedPath);
					SaveConfig();
					SetStatus("ตั้งโฟลเดอร์เกมแล้ว");
				}
				else
				{
					ShowErr("ไม่พบ gta_sa.exe ในโฟลเดอร์ที่เลือก");
				}
			}
		}
	}

	private string GetGameDir()
	{
		string text = FieldVal(txtPath);
		if (text.Length > 0 && File.Exists(Path.Combine(text, "gta_sa.exe")))
		{
			return text;
		}
		return AppDomain.CurrentDomain.BaseDirectory;
	}

	private void SaveIconFromGame(string gtaPath)
	{
		if (File.Exists(gtaPath))
		{
			base.Icon = Icon.ExtractAssociatedIcon(gtaPath);
		}
	}
}
