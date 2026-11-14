using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

internal class TurboShieldDialog : Form
{
	private readonly ICore core;
	private readonly string gameDir;
	private readonly string serverIp;
	private readonly int serverPort;
	private readonly Color colAccent;

	private ToggleSwitch swNetShield;
	private ToggleSwitch swAudioShield;
	private ToggleSwitch swGpuBoost;
	private ToggleSwitch swImmediatePresent;
	private ComboBox cmbMic;
	private Label lblNetStatus;
	private Label lblPrewarmRes;
	private Label lblAudioStatus;
	private Label lblGpuStatus;
	private Label lblResStatus;
	private RoundedBtn btnResAuto;
	private RoundedBtn btnResMax;
	private RoundedBtn btnResCons;
	private RoundedBtn btnCpuAuto;
	private RoundedBtn btnCpuPCores;
	private RoundedBtn btnCpuAll;
	private RoundedBtn btnOpenWinGpuSettings;
	private RoundedBtn btnPrewarm;
	private RoundedBtn btnTrimRam;
	private RoundedBtn btnSave;

	private ShieldDiagnostics diag;
	private string selectedResourceMode = "auto";
	private string selectedCpuAffinity = "auto";
	private List<string> micRawNames = new List<string>();
	private bool loading;
	private Font fTitle;
	private Font fHead;
	private Font fSmall;
	private Font fTiny;
	private Font fMode;
	private Font fSave;

	public TurboShieldDialog(ICore coreRef, string gameDirPath, string hostIp, int port, Color accent)
	{
		core = coreRef;
		gameDir = gameDirPath ?? "";
		serverIp = hostIp ?? "";
		serverPort = (port > 0 && port <= 65535) ? port : 7777;
		colAccent = accent;

		Text = "Sonic Turbo Shield (จัดการเน็ต • ไมค์ • การ์ดจอ • ทรัพยากร)";
		BackColor = Color.FromArgb(17, 17, 24);
		ForeColor = Color.FromArgb(235, 235, 245);
		Font = new Font("Segoe UI", 9.2f);
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.CenterParent;
		base.ClientSize = DpiHelper.Sz(708, 592);

		BuildUi();
		LoadDiagnosticsAsync();
	}

	private void BuildUi()
	{
		Color colCard = Color.FromArgb(24, 26, 38);
		Color colField = Color.FromArgb(18, 20, 30);
		Color colBorder = Color.FromArgb(40, 60, 95);
		Color colSub = Color.FromArgb(150, 165, 195);
		fTitle = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold);
		fHead = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		fSmall = new Font("Segoe UI", 8.6f);
		fTiny = new Font("Segoe UI", 8.0f);
		fMode = new Font("Segoe UI", 8.2f, FontStyle.Bold);
		fSave = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);

		Label lblHead = new Label
		{
			Text = "🛡️  SONIC TURBO SHIELD — เสถียรภาพเน็ต • ไมค์ • การ์ดจอ • ทรัพยากร (Rust Engine)",
			Font = fTitle,
			ForeColor = colAccent,
			Location = new Point(16, 12),
			AutoSize = true
		};
		Controls.Add(lblHead);

		Label lblNote = new Label
		{
			Text = "ทำงานเฉพาะใน Launcher และระหว่างเล่นเกมเท่านั้น • ไม่แก้ไข Windows Registry, Firewall หรือค่าระบบเครื่อง",
			Font = fSmall,
			ForeColor = Color.FromArgb(95, 215, 145),
			Location = new Point(18, 36),
			AutoSize = true
		};
		Controls.Add(lblNote);

		// 1. Net Shield Card (Top-Left)
		SmoothPanel pNet = new SmoothPanel(colCard, 10)
		{
			Location = new Point(16, 62),
			Size = new Size(332, 192),
			BorderColor = colBorder
		};
		Controls.Add(pNet);

		pNet.Controls.Add(new Label
		{
			Text = "🌐  1. การเชื่อมต่อเซิร์ฟเวอร์ (Net Shield)",
			Font = fHead,
			ForeColor = colAccent,
			Location = new Point(12, 10),
			AutoSize = true
		});

		swNetShield = new ToggleSwitch { Location = new Point(12, 36), OnColor = colAccent };
		pNet.Controls.Add(swNetShield);
		pNet.Controls.Add(new Label
		{
			Text = "ป้องกันหลุด UDP + ขยาย Buffer 256KB",
			Font = fSmall,
			ForeColor = Color.White,
			Location = new Point(66, 36),
			AutoSize = true
		});
		pNet.Controls.Add(new Label
		{
			Text = "ปลอดภัย Anti-Cheat 100% (ไม่แตะ RakNet packet)",
			Font = fTiny,
			ForeColor = colSub,
			Location = new Point(66, 54),
			AutoSize = true
		});

		lblNetStatus = new Label
		{
			Text = "กำลังตรวจสอบ Firewall & Alt+Tab...",
			Font = fSmall,
			ForeColor = colSub,
			Location = new Point(12, 76),
			Size = new Size(308, 58)
		};
		pNet.Controls.Add(lblNetStatus);

		lblPrewarmRes = new Label
		{
			Text = "",
			Font = fTiny,
			ForeColor = Color.FromArgb(110, 225, 160),
			Location = new Point(12, 134),
			Size = new Size(308, 18),
			AutoEllipsis = true
		};
		pNet.Controls.Add(lblPrewarmRes);

		btnPrewarm = new RoundedBtn("🔍 ทดสอบ Pre-Warm & Ping เส้นทาง", colField, Color.FromArgb(36, 44, 68), colAccent, 7, fSmall)
		{
			Location = new Point(12, 154),
			Size = new Size(308, 28)
		};
		btnPrewarm.Click += delegate { RunPrewarmProbe(); };
		pNet.Controls.Add(btnPrewarm);

		// 2. Audio / Mic Shield Card (Top-Right)
		SmoothPanel pAud = new SmoothPanel(colCard, 10)
		{
			Location = new Point(360, 62),
			Size = new Size(332, 192),
			BorderColor = colBorder
		};
		Controls.Add(pAud);

		pAud.Controls.Add(new Label
		{
			Text = "🎙️  2. ไมโครโฟน & SampVoice (Audio Shield)",
			Font = fHead,
			ForeColor = colAccent,
			Location = new Point(12, 10),
			AutoSize = true
		});

		swAudioShield = new ToggleSwitch { Location = new Point(12, 36), OnColor = colAccent };
		pAud.Controls.Add(swAudioShield);
		pAud.Controls.Add(new Label
		{
			Text = "MMCSS 'Pro Audio' + ล็อกไมค์ผ่าน BASS",
			Font = fSmall,
			ForeColor = Color.White,
			Location = new Point(66, 36),
			AutoSize = true
		});
		pAud.Controls.Add(new Label
		{
			Text = "เลือกไมค์ใช้เฉพาะในเกม (ไม่แก้ไมค์หลัก Windows)",
			Font = fTiny,
			ForeColor = colSub,
			Location = new Point(66, 54),
			AutoSize = true
		});

		pAud.Controls.Add(new Label
		{
			Text = "ไมค์หลักสำหรับในเกม (PreferredMic):",
			Font = fSmall,
			ForeColor = colSub,
			Location = new Point(12, 76),
			AutoSize = true
		});

		cmbMic = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			BackColor = colField,
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat,
			Font = fSmall,
			Location = new Point(12, 96),
			Size = new Size(308, 26)
		};
		pAud.Controls.Add(cmbMic);

		lblAudioStatus = new Label
		{
			Text = "กำลังตรวจสอบอุปกรณ์เสียงและ svlog.txt...",
			Font = fSmall,
			ForeColor = colSub,
			Location = new Point(12, 128),
			Size = new Size(308, 56)
		};
		pAud.Controls.Add(lblAudioStatus);

		// 3. GPU & D3D9 Card (Bottom-Left)
		SmoothPanel pGpu = new SmoothPanel(colCard, 10)
		{
			Location = new Point(16, 266),
			Size = new Size(332, 252),
			BorderColor = colBorder
		};
		Controls.Add(pGpu);

		pGpu.Controls.Add(new Label
		{
			Text = "🎮  3. รีดพลังการ์ดจอ & D3D9 (GPU Boost)",
			Font = fHead,
			ForeColor = colAccent,
			Location = new Point(12, 10),
			AutoSize = true
		});

		swGpuBoost = new ToggleSwitch { Location = new Point(12, 36), OnColor = colAccent };
		pGpu.Controls.Add(swGpuBoost);
		pGpu.Controls.Add(new Label
		{
			Text = "GPU Priority + ปิด EcoQoS + Hardware VP",
			Font = fSmall,
			ForeColor = Color.White,
			Location = new Point(66, 38),
			AutoSize = true
		});

		swImmediatePresent = new ToggleSwitch { Location = new Point(12, 70), OnColor = colAccent };
		pGpu.Controls.Add(swImmediatePresent);
		pGpu.Controls.Add(new Label
		{
			Text = "ลด Stutter (Immediate Present Window/FPS)",
			Font = fSmall,
			ForeColor = Color.White,
			Location = new Point(66, 72),
			AutoSize = true
		});

		lblGpuStatus = new Label
		{
			Text = "กำลังอ่านข้อมูลการ์ดจอ (Read-Only DXGI)...",
			Font = fSmall,
			ForeColor = colSub,
			Location = new Point(12, 104),
			Size = new Size(308, 98)
		};
		pGpu.Controls.Add(lblGpuStatus);

		btnOpenWinGpuSettings = new RoundedBtn("⚡ เปิด Windows Graphics Settings", colField, Color.FromArgb(36, 44, 68), Color.FromArgb(245, 195, 70), 7, fSmall)
		{
			Location = new Point(12, 212),
			Size = new Size(308, 28)
		};
		btnOpenWinGpuSettings.Click += delegate
		{
			try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:display-advancedgraphics") { UseShellExecute = true }); }
			catch { try { System.Diagnostics.Process.Start("control.exe"); } catch { } }
		};
		pGpu.Controls.Add(btnOpenWinGpuSettings);

		// 4. Smart Resource Card (Bottom-Right)
		SmoothPanel pRes = new SmoothPanel(colCard, 10)
		{
			Location = new Point(360, 266),
			Size = new Size(332, 252),
			BorderColor = colBorder
		};
		Controls.Add(pRes);

		pRes.Controls.Add(new Label
		{
			Text = "⚡  4. ทรัพยากรเครื่องอัจฉริยะ (CPU / RAM)",
			Font = fHead,
			ForeColor = colAccent,
			Location = new Point(12, 10),
			AutoSize = true
		});

		pRes.Controls.Add(new Label
		{
			Text = "โหมดจัดสรร RAM สตรีม (stream.ini / LimitAdjuster):",
			Font = fSmall,
			ForeColor = colSub,
			Location = new Point(12, 32),
			AutoSize = true
		});

		btnResAuto = MakeModeBtn("Auto (แนะนำ)", 12, 52, 98);
		btnResAuto.Click += delegate { SelectResourceMode("auto"); };
		pRes.Controls.Add(btnResAuto);

		btnResMax = MakeModeBtn("Max (1 GB)", 116, 52, 98);
		btnResMax.Click += delegate { SelectResourceMode("max"); };
		pRes.Controls.Add(btnResMax);

		btnResCons = MakeModeBtn("ประหยัด (512MB)", 220, 52, 100);
		btnResCons.Click += delegate { SelectResourceMode("conservative"); };
		pRes.Controls.Add(btnResCons);

		pRes.Controls.Add(new Label
		{
			Text = "โหมดคอร์ CPU Affinity (Intel P-Core / AMD CCD):",
			Font = fSmall,
			ForeColor = colSub,
			Location = new Point(12, 86),
			AutoSize = true
		});

		btnCpuAuto = MakeModeBtn("Auto (Hybrid)", 12, 106, 100);
		btnCpuAuto.Click += delegate { SelectCpuAffinity("auto"); };
		pRes.Controls.Add(btnCpuAuto);

		btnCpuPCores = MakeModeBtn("P-Cores", 116, 106, 98);
		btnCpuPCores.Click += delegate { SelectCpuAffinity("p_cores"); };
		pRes.Controls.Add(btnCpuPCores);

		btnCpuAll = MakeModeBtn("ทุกคอร์ (All)", 218, 106, 102);
		btnCpuAll.Click += delegate { SelectCpuAffinity("all"); };
		pRes.Controls.Add(btnCpuAll);

		lblResStatus = new Label
		{
			Text = "กำลังอ่านสถานะ CPU / RAM...",
			Font = fSmall,
			ForeColor = colSub,
			Location = new Point(12, 140),
			Size = new Size(308, 64)
		};
		pRes.Controls.Add(lblResStatus);

		btnTrimRam = new RoundedBtn("🧹 คืน RAM ส่วนเกินของ Launcher ตอนนี้", colField, Color.FromArgb(36, 44, 68), Color.FromArgb(245, 195, 70), 7, fSmall)
		{
			Location = new Point(12, 212),
			Size = new Size(308, 28)
		};
		btnTrimRam.Click += delegate
		{
			long after = core.TrimLauncherMemory();
			LoadDiagnosticsAsync();
			MessageBox.Show("คืน Working Set ของ Launcher แล้ว (ปัจจุบันใช้ ~" + after + " MB)", "Smart Resource Trim", MessageBoxButtons.OK, MessageBoxIcon.Information);
		};
		pRes.Controls.Add(btnTrimRam);

		// Bottom Actions
		btnSave = new RoundedBtn("💾  บันทึกการตั้งค่าทั้งหมดลง SonicGuard.ini", colAccent, ControlPaint.Light(colAccent, 0.2f), Color.FromArgb(11, 14, 20), 8, fSave)
		{
			Location = new Point(316, 538),
			Size = new Size(268, 36)
		};
		btnSave.Click += delegate { SaveSettings(); };
		Controls.Add(btnSave);

		RoundedBtn btnClose = new RoundedBtn("ปิด (Esc)", colField, Color.FromArgb(36, 44, 68), colSub, 8, fHead)
		{
			Location = new Point(594, 538),
			Size = new Size(98, 36)
		};
		btnClose.Click += delegate { Close(); };
		Controls.Add(btnClose);
		base.CancelButton = btnClose;

		DpiHelper.ScaleControlTree(this);
	}

	private RoundedBtn MakeModeBtn(string text, int x, int y, int w)
	{
		return new RoundedBtn(text, Color.FromArgb(18, 20, 30), Color.FromArgb(36, 44, 68), Color.FromArgb(220, 230, 245), 6, fMode)
		{
			Location = new Point(x, y),
			Size = new Size(w, 28)
		};
	}

	private void SelectResourceMode(string mode)
	{
		selectedResourceMode = mode;
		UpdateResourceModeButtons();
	}

	private void UpdateResourceModeButtons()
	{
		Color cOff = Color.FromArgb(18, 20, 30);
		Color cOffHi = Color.FromArgb(36, 44, 68);
		Color cBorder = Color.FromArgb(40, 60, 95);
		Color cHi = ControlPaint.Light(colAccent, 0.2f);
		Color darkTxt = Color.FromArgb(11, 14, 20);
		Color normTxt = Color.FromArgb(220, 230, 245);

		RoundedBtn[] btns = new RoundedBtn[] { btnResAuto, btnResMax, btnResCons };
		string[] keys = new string[] { "auto", "max", "conservative" };
		for (int i = 0; i < btns.Length; i++)
		{
			if (btns[i] == null) continue;
			bool active = string.Equals(selectedResourceMode, keys[i], StringComparison.OrdinalIgnoreCase);
			btns[i].SetPalette(active ? colAccent : cOff, active ? cHi : cOffHi);
			btns[i].BorderColor = active ? colAccent : cBorder;
			btns[i].ForeColor = active ? darkTxt : normTxt;
			btns[i].Invalidate();
		}
	}

	private void SelectCpuAffinity(string mode)
	{
		selectedCpuAffinity = mode;
		UpdateCpuAffinityButtons();
	}

	private void UpdateCpuAffinityButtons()
	{
		Color cOff = Color.FromArgb(18, 20, 30);
		Color cOffHi = Color.FromArgb(36, 44, 68);
		Color cBorder = Color.FromArgb(40, 60, 95);
		Color cHi = ControlPaint.Light(colAccent, 0.2f);
		Color darkTxt = Color.FromArgb(11, 14, 20);
		Color normTxt = Color.FromArgb(220, 230, 245);

		RoundedBtn[] btns = new RoundedBtn[] { btnCpuAuto, btnCpuPCores, btnCpuAll };
		string[] keys = new string[] { "auto", "p_cores", "all" };
		for (int i = 0; i < btns.Length; i++)
		{
			if (btns[i] == null) continue;
			bool active = string.Equals(selectedCpuAffinity, keys[i], StringComparison.OrdinalIgnoreCase);
			btns[i].SetPalette(active ? colAccent : cOff, active ? cHi : cOffHi);
			btns[i].BorderColor = active ? colAccent : cBorder;
			btns[i].ForeColor = active ? darkTxt : normTxt;
			btns[i].Invalidate();
		}
	}

	private void LoadDiagnosticsAsync()
	{
		loading = true;
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				ShieldDiagnostics d = core.GetShieldDiagnostics(gameDir, serverIp, serverPort);
				if (!IsDisposed)
				{
					BeginInvoke(new Action(delegate
					{
						ApplyDiagnostics(d);
					}));
					return;
				}
			}
			catch
			{
			}
			if (!IsDisposed)
			{
				try { BeginInvoke(new Action(delegate { if (!IsDisposed) loading = false; })); } catch { }
			}
		});
	}

	private void ApplyDiagnostics(ShieldDiagnostics d)
	{
		try
		{
			if (IsDisposed || d == null) return;
			diag = d;

			swNetShield.SetCheckedSilent(d.NetShield);
			swAudioShield.SetCheckedSilent(d.AudioShield);
			swGpuBoost.SetCheckedSilent(d.GpuBoost);
			swImmediatePresent.SetCheckedSilent(d.ForceImmediatePresent);
			selectedResourceMode = string.IsNullOrEmpty(d.ResourceMode) ? "auto" : d.ResourceMode.ToLowerInvariant();
			UpdateResourceModeButtons();
			selectedCpuAffinity = string.IsNullOrEmpty(d.CpuAffinity) ? "auto" : d.CpuAffinity.ToLowerInvariant();
			UpdateCpuAffinityButtons();

		// 1. Net Status
		string voiceStr = (d.LastVoicePort > 0) ? ("\r\n• Voice Port ล่าสุด: " + d.LastVoiceServer + ":" + d.LastVoicePort) : "";
		string altStr = d.AltTabNoPause ? "• พับจอไม่หลุด: เปิดอยู่แล้ว" : "• คำเตือน Alt+Tab: " + d.AltTabNote;
		lblNetStatus.Text = "• " + d.FirewallSummary + "\r\n" + altStr + voiceStr;
		lblNetStatus.ForeColor = d.FirewallGtaBlocked ? Color.FromArgb(255, 110, 110) : (d.AltTabNoPause ? Color.FromArgb(140, 215, 165) : Color.FromArgb(245, 200, 90));

		// 2. Mic list & Audio status
		cmbMic.Items.Clear();
		micRawNames.Clear();
		cmbMic.Items.Add("อัตโนมัติ (ปล่อยให้ตัวเกมเลือกเอง)");
		micRawNames.Add("");
		int selIdx = 0;
		foreach (MicDeviceInfo m in d.Mics)
		{
			string tag = m.IsVirtual ? " [Virtual]" : " [แนะนำ]";
			cmbMic.Items.Add(m.Name + tag);
			micRawNames.Add(m.Name);
			if (!string.IsNullOrEmpty(d.PreferredMic) && m.Name.IndexOf(d.PreferredMic, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				selIdx = micRawNames.Count - 1;
			}
		}
		if (!string.IsNullOrEmpty(d.PreferredMic) && selIdx == 0)
		{
			cmbMic.Items.Add(d.PreferredMic + " (บันทึกไว้เดิม)");
			micRawNames.Add(d.PreferredMic);
			selIdx = micRawNames.Count - 1;
		}
		cmbMic.SelectedIndex = selIdx;
		lblAudioStatus.Text = d.AudioRecommendation;
		lblAudioStatus.ForeColor = d.MicConsentAllowed ? Color.FromArgb(165, 190, 215) : Color.FromArgb(255, 110, 110);

		// 3. GPU Status (Read-Only)
		lblGpuStatus.Text = d.GpuSummary + "\r\n• Windows Preference: " + d.WindowsGpuPref;
		lblGpuStatus.ForeColor = d.IsHybridGpu && !d.WindowsGpuPref.StartsWith("High")
			? Color.FromArgb(245, 205, 95)
			: Color.FromArgb(165, 205, 190);

		// 4. Resources Status
		lblResStatus.Text = string.Format(
			"• CPU: {0} Logical Threads  |  RAM เครื่องว่าง: {1} / {2} MB\r\n• โควตา stream.ini ที่เหมาะสมตอนนี้: {3} MB\r\n• Launcher ใช้ RAM ปัจจุบัน: {4} MB (ระบบ Trim อัตโนมัติตอนเปิดเกม)",
			d.LogicalCores, d.AvailRamMb, d.TotalRamMb, d.RecommendedStreamMb, d.LauncherWorkingSetMb);
		}
		finally
		{
			loading = false;
		}
	}

	private void RunPrewarmProbe()
	{
		if (string.IsNullOrEmpty(serverIp))
		{
			lblPrewarmRes.Text = "⚠ ยังไม่มี IP เซิร์ฟเวอร์ปลายทางในช่องเชื่อมต่อ";
			lblPrewarmRes.ForeColor = Color.FromArgb(245, 195, 70);
			return;
		}
		btnPrewarm.Enabled = false;
		lblPrewarmRes.Text = "กำลังอุ่น DNS & ส่ง SA-MP NAT Probe...";
		ThreadPool.QueueUserWorkItem(delegate
		{
			PrewarmReport rep = null;
			try
			{
				rep = core.PrewarmServer(serverIp, serverPort);
			}
			catch
			{
			}
			try
			{
				if (!IsDisposed)
				{
					BeginInvoke(new Action(delegate
					{
						if (IsDisposed) return;
						btnPrewarm.Enabled = true;
						if (rep != null)
						{
							lblPrewarmRes.Text = rep.Summary;
							lblPrewarmRes.ForeColor = (rep.GamePingMs >= 0) ? Color.FromArgb(110, 225, 160) : Color.FromArgb(245, 200, 90);
						}
						else
						{
							lblPrewarmRes.Text = "ทดสอบเส้นทางไม่สำเร็จ";
							lblPrewarmRes.ForeColor = Color.FromArgb(255, 110, 110);
						}
					}));
					return;
				}
			}
			catch
			{
			}
			if (!IsDisposed)
			{
				try { BeginInvoke(new Action(delegate { if (!IsDisposed) btnPrewarm.Enabled = true; })); } catch { }
			}
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			Font[] fonts = new Font[] { fTitle, fHead, fSmall, fTiny, fMode, fSave };
			foreach (Font f in fonts)
			{
				if (f != null) { try { f.Dispose(); } catch { } }
			}
			fTitle = fHead = fSmall = fTiny = fMode = fSave = null;
		}
		base.Dispose(disposing);
	}

	private void SaveSettings()
	{
		if (loading) return;
		Dictionary<string, string> updates = new Dictionary<string, string>();
		updates["NetShield"] = swNetShield.Checked ? "1" : "0";
		updates["AudioShield"] = swAudioShield.Checked ? "1" : "0";
		string pickedMic = "";
		if (cmbMic.SelectedIndex >= 0 && cmbMic.SelectedIndex < micRawNames.Count)
		{
			pickedMic = micRawNames[cmbMic.SelectedIndex];
		}
		updates["PreferredMic"] = pickedMic;
		updates["GpuBoost"] = swGpuBoost.Checked ? "1" : "0";
		updates["ForceImmediatePresent"] = swImmediatePresent.Checked ? "1" : "0";
		updates["ResourceMode"] = selectedResourceMode;
		updates["CpuAffinity"] = selectedCpuAffinity;

		string err = core.SaveShieldIni(gameDir, updates);
		if (!string.IsNullOrEmpty(err))
		{
			MessageBox.Show("บันทึกการตั้งค่าไม่สำเร็จ:\r\n" + err, "Sonic Turbo Shield", MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}
		base.DialogResult = DialogResult.OK;
		Close();
	}
}
