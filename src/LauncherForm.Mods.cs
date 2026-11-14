using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal partial class LauncherForm : Form
{
	private ToggleSwitch swSonicGuard;
	private ToggleSwitch swBorderless;
	private ToggleSwitch swModSafeMode;
	private CheckedListBox clbModFolders;
	private Label lblGuardStatus;
	private Label lblTelemetryInfo;
	private Label lblModloaderInfo;
	private RoundedBtn btnViewCrash;
	private RoundedBtn btnTurboShield;

	private int modsRefreshSeq = 0;

	private void OpenTurboShieldDialog()
	{
		string host = FieldVal(txtIp);
		int port = 7777;
		int parsed;
		if (int.TryParse(FieldVal(txtPort), out parsed) && parsed >= 1 && parsed <= 65535)
		{
			port = parsed;
		}
		using (TurboShieldDialog dlg = new TurboShieldDialog(core, GetGameDir(), host, port, colAccent))
		{
			if (dlg.ShowDialog(this) == DialogResult.OK)
			{
				RefreshModsPage();
				if (pnlSet != null && pnlSet.Visible)
				{
					RunHealthCheck();
				}
				ToastOk("บันทึกการตั้งค่า Turbo Shield เรียบร้อย (มีผลเมื่อเข้าเกมครั้งถัดไป)");
			}
		}
	}
	private ModLoaderState cachedModState;
	private bool modsPageLoading;
	private bool assetScanBusy;

	private sealed class ModFolderItem
	{
		public string FolderName;
		public string Display;
		public int Priority = 50;
		public override string ToString()
		{
			return Display;
		}
	}

	private void BuildModsPage()
	{
		// Left Card: Sonic Guard & Borderless
		SmoothPanel pnlGuardCard = new SmoothPanel(colCard, 10);
		pnlGuardCard.Location = new Point(0, 0);
		pnlGuardCard.Size = new Size(326, 436);
		pnlGuardCard.BorderColor = Color.FromArgb(40, 60, 95);
		pnlMods.Controls.Add(pnlGuardCard);

		AddLbl(pnlGuardCard, "🛡️  SONIC GUARD & WINDOW", fSub, 14, 12, colAccent);
		lblGuardStatus = AddLbl(pnlGuardCard, "กำลังตรวจสอบสถานะ...", fSmall, 14, 36, colSub);

		swSonicGuard = new ToggleSwitch();
		swSonicGuard.Location = new Point(14, 66);
		swSonicGuard.CheckedChanged += delegate
		{
			if (!modsPageLoading)
			{
				ToggleSonicGuardAsi(swSonicGuard.Checked);
			}
		};
		pnlGuardCard.Controls.Add(swSonicGuard);
		AddLbl(pnlGuardCard, "เปิดใช้งาน SonicGuard.asi", fSmall, 68, 66, colText);
		AddLbl(pnlGuardCard, "จับสาเหตุแครช + ไทม์เมอร์ 1ms ลดกระตุก", fSmall, 68, 84, colSub);

		swBorderless = new ToggleSwitch();
		swBorderless.Location = new Point(14, 112);
		swBorderless.CheckedChanged += delegate
		{
			if (!modsPageLoading)
			{
				Dictionary<string, string> upd = new Dictionary<string, string>();
				upd["Borderless"] = swBorderless.Checked ? "1" : "0";
				string err = core.SaveShieldIni(GetGameDir(), upd);
				if (!string.IsNullOrEmpty(err))
				{
					swBorderless.SetCheckedSilent(!swBorderless.Checked);
					ShowErr(err);
					return;
				}
				ToastOk(swBorderless.Checked
					? "เปิดโหมด Borderless Windowed แล้ว (ใช้ตอนเข้าเกมครั้งถัดไป)"
					: "ปิดโหมด Borderless Windowed แล้ว");
			}
		};
		pnlGuardCard.Controls.Add(swBorderless);
		AddLbl(pnlGuardCard, "โหมดไร้ขอบ (Borderless Windowed)", fSmall, 68, 112, colText);
		AddLbl(pnlGuardCard, "ป้องกันจอดำ/ค้างเวลาพับจอ Alt+Tab", fSmall, 68, 130, colSub);

		SmoothPanel pnlTele = new SmoothPanel(colField, 8);
		pnlTele.Location = new Point(14, 154);
		pnlTele.Size = new Size(298, 90);
		pnlTele.BorderColor = Color.FromArgb(35, 52, 82);
		pnlGuardCard.Controls.Add(pnlTele);

		AddLbl(pnlTele, "📊 สถิติ RAM & Shield ล่าสุด (Telemetry)", fSmall, 12, 6, colAccent);
		lblTelemetryInfo = AddLbl(pnlTele, "ยังไม่มีข้อมูล (เล่นเกมพร้อม SonicGuard เพื่อบันทึก)", fSmall, 12, 26, colSub);
		lblTelemetryInfo.AutoSize = false;
		lblTelemetryInfo.Size = new Size(274, 60);

		RoundedBtn btnScanAssets = MakeMiniBtn("⚡ สแกน DFF / TXD", 14, 250, 145, 36);
		btnScanAssets.BorderColor = colAccent;
		btnScanAssets.Click += delegate
		{
			RunAssetScanDialog();
		};
		pnlGuardCard.Controls.Add(btnScanAssets);

		btnTurboShield = MakeMiniBtn("🛡️ Turbo Shield", 167, 250, 145, 36);
		btnTurboShield.BorderColor = colAccent;
		btnTurboShield.Click += delegate
		{
			OpenTurboShieldDialog();
		};
		pnlGuardCard.Controls.Add(btnTurboShield);

		Label lblScanHint = AddLbl(pnlGuardCard, "ตรวจม็อด/เท็กซ์เจอร์ซ้ำ • ตั้งค่า Shield เน็ต/ไมค์/GPU/RAM", fSmall, 14, 290, colSub);
		lblScanHint.AutoSize = false;
		lblScanHint.Size = new Size(298, 32);

		btnViewCrash = MakeMiniBtn("⚠️  ดูรายงานแครชล่าสุด", 14, 328, 298, 36);
		btnViewCrash.BorderColor = Color.FromArgb(243, 156, 18);
		btnViewCrash.Click += delegate
		{
			ShowLatestCrashDialog(true);
		};
		pnlGuardCard.Controls.Add(btnViewCrash);

		RoundedBtn btnOpenScripts = MakeMiniBtn("📂 เปิด scripts", 14, 384, 144, 34);
		btnOpenScripts.BorderColor = Color.FromArgb(40, 60, 95);
		btnOpenScripts.Click += delegate
		{
			OpenSubFolder("scripts");
		};
		pnlGuardCard.Controls.Add(btnOpenScripts);

		RoundedBtn btnOpenModloader = MakeMiniBtn("📂 เปิด modloader", 168, 384, 144, 34);
		btnOpenModloader.BorderColor = Color.FromArgb(40, 60, 95);
		btnOpenModloader.Click += delegate
		{
			OpenSubFolder("modloader");
		};
		pnlGuardCard.Controls.Add(btnOpenModloader);

		// Right Card: ModLoader Manager
		SmoothPanel pnlMlCard = new SmoothPanel(colCard, 10);
		pnlMlCard.Location = new Point(340, 0);
		pnlMlCard.Size = new Size(328, 436);
		pnlMlCard.BorderColor = Color.FromArgb(40, 60, 95);
		pnlMods.Controls.Add(pnlMlCard);

		AddLbl(pnlMlCard, "📁  MODLOADER MANAGER", fSub, 14, 12, colAccent);
		lblModloaderInfo = AddLbl(pnlMlCard, "กำลังโหลดรายชื่อม็อด...", fSmall, 14, 36, colSub);

		swModSafeMode = new ToggleSwitch();
		swModSafeMode.Location = new Point(14, 62);
		swModSafeMode.CheckedChanged += delegate
		{
			if (!modsPageLoading)
			{
				SaveCurrentModLoaderConfig();
			}
		};
		pnlMlCard.Controls.Add(swModSafeMode);
		AddLbl(pnlMlCard, "🔴 Safe Mode (ปิดม็อดทั้งหมดชั่วคราว)", fSmall, 68, 66, colYellow);

		AddLbl(pnlMlCard, "ติ๊กถูกหน้าโฟลเดอร์ม็อดที่ต้องการเปิดใช้งาน:", fSmall, 14, 94, colSub);

		clbModFolders = new CheckedListBox();
		clbModFolders.Location = new Point(14, 116);
		clbModFolders.Size = new Size(300, 218);
		clbModFolders.BackColor = colField;
		clbModFolders.ForeColor = colText;
		clbModFolders.BorderStyle = BorderStyle.FixedSingle;
		clbModFolders.Font = fSmall;
		clbModFolders.CheckOnClick = true;
		clbModFolders.IntegralHeight = false;
		pnlMlCard.Controls.Add(clbModFolders);

		RoundedBtn btnModPriority = MakeMiniBtn("⚡ ปรับลำดับ Priority (ปกติ 50 -> สูง 80 -> ต่ำ 20)", 14, 344, 300, 30);
		btnModPriority.BorderColor = colAccent;
		btnModPriority.Click += delegate
		{
			CycleSelectedModPriority();
		};
		pnlMlCard.Controls.Add(btnModPriority);

		RoundedBtn btnSaveMods = MakeMiniBtn("💾  บันทึกการตั้งค่าม็อด", 14, 384, 196, 34);
		btnSaveMods.BorderColor = colOk;
		btnSaveMods.Click += delegate
		{
			SaveCurrentModLoaderConfig();
		};
		pnlMlCard.Controls.Add(btnSaveMods);

		RoundedBtn btnRefreshMods = MakeMiniBtn("↻  รีเฟรช", 218, 384, 96, 34);
		btnRefreshMods.BorderColor = colAccent;
		btnRefreshMods.Click += delegate
		{
			RefreshModsPage();
		};
		pnlMlCard.Controls.Add(btnRefreshMods);
	}

	private void OpenSubFolder(string folderName)
	{
		string dir = Path.Combine(GetGameDir(), folderName);
		if (!Directory.Exists(dir))
		{
			try
			{
				Directory.CreateDirectory(dir);
			}
			catch (Exception ex)
			{
				ShowErr("ไม่สามารถเปิดโฟลเดอร์ได้: " + ex.Message);
				return;
			}
		}
		try
		{
			Process.Start("explorer.exe", dir);
		}
		catch (Exception ex)
		{
			ShowErr("เปิดโฟลเดอร์ไม่สำเร็จ: " + ex.Message);
		}
	}

	private void RefreshModsPage()
	{
		modsPageLoading = true;
		int reqSeq = ++modsRefreshSeq;
		try
		{
			string gameDir = GetGameDir();
			string asiPath = Path.Combine(gameDir, "scripts", "SonicGuard.asi");
			string asiDisabled = Path.Combine(gameDir, "scripts", "SonicGuard.asi.disabled");

			bool asiExists = File.Exists(asiPath);
			bool asiDisExists = File.Exists(asiDisabled);

			if (asiExists)
			{
				lblGuardStatus.Text = "✓ SonicGuard.asi พร้อมทำงาน (scripts\\SonicGuard.asi)";
				lblGuardStatus.ForeColor = colOk;
				swSonicGuard.SetCheckedSilent(true);
			}
			else if (asiDisExists)
			{
				lblGuardStatus.Text = "⏸ SonicGuard.asi ถูกปิดชั่วคราว (.disabled)";
				lblGuardStatus.ForeColor = colYellow;
				swSonicGuard.SetCheckedSilent(false);
			}
			else
			{
				lblGuardStatus.Text = "⚠ ยังไม่พบ scripts\\SonicGuard.asi";
				lblGuardStatus.ForeColor = Color.FromArgb(255, 110, 110);
				swSonicGuard.SetCheckedSilent(false);
			}

			ThreadPool.QueueUserWorkItem(delegate
			{
				ShieldDiagnostics diag = null;
				try { diag = core.GetShieldDiagnostics(gameDir, "", 0); } catch { }
				try
				{
					if (!base.IsDisposed)
					{
						BeginInvoke((MethodInvoker)delegate
						{
							if (base.IsDisposed || reqSeq != modsRefreshSeq) return;
							if (diag != null)
							{
								swSonicGuard.SetCheckedSilent(asiExists && diag.Enable);
								swBorderless.SetCheckedSilent(diag.Borderless);
							}
						});
					}
				}
				catch { }
			});

			// Guard Runtime Status (Telemetry & Crash)
			GuardRuntimeStatus rt = core.GetGuardStatus(gameDir);
			if (rt != null && rt.Telemetry != null && rt.Telemetry.WorkingSetMb > 0)
			{
				List<string> badges = new List<string>();
				if (rt.Telemetry.NetShieldActive) badges.Add("Net(" + rt.Telemetry.TunedUdpSockets + ")");
				if (rt.Telemetry.AudioShieldActive) badges.Add("Mic");
				if (rt.Telemetry.GpuShieldActive) badges.Add("GPU");
				if (rt.Telemetry.BorderlessActive) badges.Add("Borderless");
				string shieldText = badges.Count > 0 ? string.Join(" • ", badges.ToArray()) : "รอดึงข้อมูล";
				string micText = string.IsNullOrEmpty(rt.Telemetry.ActiveMic) ? "Windows Default" : rt.Telemetry.ActiveMic;
				if (micText.Length > 26) micText = micText.Substring(0, 23) + "...";
				string freeText = (rt.Telemetry.SystemFreeRamMb > 0) ? (" | ว่าง " + rt.Telemetry.SystemFreeRamMb + "MB") : "";
				lblTelemetryInfo.Text = string.Format(
					"RAM: {0}/{1}MB | VM: {2}MB{3}\r\nไมค์: {4}\r\nShield: {5}",
					rt.Telemetry.WorkingSetMb,
					rt.Telemetry.PeakWorkingSetMb,
					rt.Telemetry.VirtualMemMb,
					freeText,
					micText,
					shieldText);
				lblTelemetryInfo.ForeColor = (rt.Telemetry.VirtualMemMb > 3100) ? colYellow : colText;
			}
			else
			{
				lblTelemetryInfo.Text = "ยังไม่มีข้อมูล (เล่นเกมพร้อม SonicGuard เพื่อบันทึก)";
				lblTelemetryInfo.ForeColor = colSub;
			}

			if (rt != null && rt.HasCrash && rt.Crash != null)
			{
				btnViewCrash.Text = "⚠️  พบประวัติเกมแครชล่าสุด! (คลิกดูสาเหตุ)";
				btnViewCrash.BorderColor = Color.FromArgb(255, 70, 90);
			}
			else
			{
				btnViewCrash.Text = "✓  ไม่พบรายงานแครชล่าสุด";
				btnViewCrash.BorderColor = Color.FromArgb(40, 60, 95);
			}

			// ModLoader state
			cachedModState = core.GetModLoaderState(gameDir);
			clbModFolders.Items.Clear();
			if (cachedModState != null && cachedModState.Installed)
			{
				swModSafeMode.SetCheckedSilent(cachedModState.IgnoreAll);
				lblModloaderInfo.Text = string.Format(
					"โปรไฟล์: {0}  •  พบทั้งหมด {1} โฟลเดอร์",
					cachedModState.Profile,
					cachedModState.Folders.Count);
				lblModloaderInfo.ForeColor = cachedModState.IgnoreAll ? colYellow : colOk;

				foreach (ModLoaderFolder f in cachedModState.Folders)
				{
					double mb = f.TotalBytes / (1024.0 * 1024.0);
					ModFolderItem item = new ModFolderItem();
					item.FolderName = f.Name;
					item.Priority = f.Priority;
					item.Display = string.Format("{0} [P:{1}]  ({2} ไฟล์ • {3:0.0} MB)", f.Name, f.Priority, f.FileCount, mb);
					int idx = clbModFolders.Items.Add(item);
					clbModFolders.SetItemChecked(idx, f.Enabled);
				}
			}
			else
			{
				swModSafeMode.SetCheckedSilent(false);
				lblModloaderInfo.Text = "ไม่พบโฟลเดอร์ modloader ในตัวเกม";
				lblModloaderInfo.ForeColor = colSub;
			}
		}
		catch (Exception ex)
		{
			lblGuardStatus.Text = "โหลดข้อมูลผิดพลาด: " + ex.Message;
		}
		finally
		{
			modsPageLoading = false;
		}
	}

	private void ToggleSonicGuardAsi(bool enable)
	{
		string gameDir = GetGameDir();
		string scriptsDir = Path.Combine(gameDir, "scripts");
		string asiPath = Path.Combine(scriptsDir, "SonicGuard.asi");
		string disPath = Path.Combine(scriptsDir, "SonicGuard.asi.disabled");

		try
		{
			if (!Directory.Exists(scriptsDir))
			{
				Directory.CreateDirectory(scriptsDir);
			}
			if (enable)
			{
				if (!File.Exists(asiPath) && File.Exists(disPath))
				{
					File.Move(disPath, asiPath);
				}
				Dictionary<string, string> upd = new Dictionary<string, string>();
				upd["Enable"] = "1";
				core.SaveShieldIni(gameDir, upd);
				if (File.Exists(asiPath))
				{
					ToastOk("เปิดใช้งาน SonicGuard.asi เรียบร้อย");
				}
				else
				{
					Toast("เปิด Enable=1 แล้ว แต่ไม่พบไฟล์ scripts\\SonicGuard.asi");
				}
			}
			else
			{
				if (File.Exists(asiPath))
				{
					if (File.Exists(disPath)) File.Delete(disPath);
					File.Move(asiPath, disPath);
				}
				Dictionary<string, string> upd = new Dictionary<string, string>();
				upd["Enable"] = "0";
				core.SaveShieldIni(gameDir, upd);
				ToastOk("ปิดใช้งาน SonicGuard.asi เรียบร้อย");
			}
		}
		catch (Exception ex)
		{
			ShowErr("ไม่สามารถสลับสถานะ SonicGuard.asi (อาจกำลังรันเกมอยู่): " + ex.Message);
		}
		RefreshModsPage();
	}

	private void CycleSelectedModPriority()
	{
		ModFolderItem item = clbModFolders.SelectedItem as ModFolderItem;
		if (item == null)
		{
			Toast("กรุณาคลิกเลือกโฟลเดอร์ม็อดในรายการก่อน");
			return;
		}
		int nextPrio = (item.Priority == 50) ? 80 : ((item.Priority == 80) ? 20 : 50);
		string err = core.SetModLoaderPriority(GetGameDir(), item.FolderName, nextPrio);
		if (!string.IsNullOrEmpty(err))
		{
			ShowErr("ปรับลำดับ Priority ไม่สำเร็จ:\r\n" + err);
			return;
		}
		int prevIdx = clbModFolders.SelectedIndex;
		RefreshModsPage();
		if (prevIdx >= 0 && prevIdx < clbModFolders.Items.Count)
		{
			clbModFolders.SelectedIndex = prevIdx;
		}
		ToastOk("ตั้ง Priority ของ \"" + item.FolderName + "\" เป็น " + nextPrio + " แล้ว");
	}

	private void SaveCurrentModLoaderConfig()
	{
		string gameDir = GetGameDir();
		List<string> disabled = new List<string>();
		for (int i = 0; i < clbModFolders.Items.Count; i++)
		{
			if (!clbModFolders.GetItemChecked(i))
			{
				ModFolderItem item = clbModFolders.Items[i] as ModFolderItem;
				if (item != null)
				{
					disabled.Add(item.FolderName);
				}
			}
		}
		string err = core.SaveModLoaderState(gameDir, swModSafeMode.Checked, disabled);
		if (!string.IsNullOrEmpty(err))
		{
			ShowErr("บันทึก ModLoader ไม่สำเร็จ:\r\n" + err);
			return;
		}
		if (swModSafeMode.Checked)
		{
			ToastOk("เปิด Safe Mode แล้ว (ModLoader จะข้ามม็อดทั้งหมดในการเข้าเกมครั้งหน้า)");
		}
		else
		{
			ToastOk("บันทึกการตั้งค่า ModLoader เรียบร้อย (ปิดใช้งาน " + disabled.Count + " โฟลเดอร์)");
		}
		RefreshModsPage();
	}

	private void RunAssetScanDialog()
	{
		if (assetScanBusy)
		{
			return;
		}
		assetScanBusy = true;
		SetStatus("กำลังสแกนไฟล์โมเดลและเท็กซ์เจอร์ (DFF / TXD / IMG)...");
		string gameDir = GetGameDir();

		ThreadPool.QueueUserWorkItem(delegate
		{
			AssetScanReport report = null;
			string scanErr = null;
			try
			{
				report = core.ScanAssets(gameDir);
			}
			catch (Exception ex)
			{
				scanErr = ex.Message;
			}

			if (!base.IsDisposed)
			{
				try
				{
					BeginInvoke((MethodInvoker)delegate
					{
						assetScanBusy = false;
						SetStatus("พร้อมเชื่อมต่อ");
						if (!string.IsNullOrEmpty(scanErr))
						{
							ShowErr("สแกนไฟล์ผิดพลาด: " + scanErr);
							return;
						}
						ShowAssetReportDialog(report);
					});
				}
				catch
				{
					assetScanBusy = false;
				}
			}
		});
	}

	private void ShowAssetReportDialog(AssetScanReport rep)
	{
		if (rep == null)
		{
			Toast("ไม่พบข้อมูลการสแกน");
			return;
		}
		using (Form dlg = new Form())
		{
			dlg.Text = "รายงานตรวจสอบไฟล์เกม (Asset Scanner)";
			dlg.ClientSize = DpiHelper.Sz(640, 440);
			dlg.StartPosition = FormStartPosition.CenterParent;
			dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
			dlg.MaximizeBox = false;
			dlg.MinimizeBox = false;
			dlg.BackColor = colBg;
			dlg.ForeColor = colText;

			string truncNote = rep.TruncatedIssues ? "  [แสดงเฉพาะ 500 รายการแรก]" : "";
			string summary = string.Format(
				"สแกนทั้งหมด {0} ไฟล์  (DFF: {1}  •  TXD: {2}  •  COL: {3}  •  IFP: {4}  •  IMG: {5}  •  Textures: {6})\r\n" +
				"พบข้อผิดพลาดร้ายแรง: {7}  |  คำเตือน (ขนาดใหญ่): {8}  |  ไฟล์ชื่อซ้ำใน ModLoader: {9}{10}",
				rep.ScannedFiles, rep.DffCount, rep.TxdCount, rep.ColCount, rep.IfpCount, rep.ImgCount, rep.TextureCount,
				rep.ErrorCount, rep.WarnCount, rep.DuplicateCount, truncNote);

			Color sumCol = (rep.ErrorCount > 0) ? Color.FromArgb(255, 99, 99) : ((rep.WarnCount > 0 || rep.DuplicateCount > 0 || rep.TruncatedIssues) ? colYellow : colOk);
			Label lblSum = AddLbl(dlg, summary, fSmall, 16, 14, sumCol);
			lblSum.AutoSize = false;
			lblSum.Size = new Size(608, 38);

			ListBox lb = new ListBox();
			lb.Location = new Point(16, 60);
			lb.Size = new Size(608, 316);
			lb.BackColor = colCard;
			lb.ForeColor = colText;
			lb.BorderStyle = BorderStyle.FixedSingle;
			lb.Font = fSmall;
			lb.HorizontalScrollbar = true;

			if (rep.TruncatedIssues)
			{
				lb.Items.Add("⚠ [TRUNCATED] พบปัญหามากเกิน 500 รายการ ระบบตัดทอนรายการที่เหลือเพื่อลดการใช้หน่วยความจำ");
			}
			if (rep.Issues.Count == 0)
			{
				lb.Items.Add("✓ ยอดเยี่ยม! ไม่พบโมเดลเสีย เท็กซ์เจอร์ขนาดใหญ่เกิน หรือไฟล์ทับซ้อนใน ModLoader");
			}
			else
			{
				foreach (AssetIssue iss in rep.Issues)
				{
					string tag = (iss.Severity == "error") ? "[✖ ERROR]" : "[⚠ WARN]";
					lb.Items.Add(string.Format("{0} [{1}] {2} — {3}", tag, iss.Kind.ToUpperInvariant(), iss.RelPath, iss.Message));
				}
			}
			dlg.Controls.Add(lb);

			RoundedBtn btnCopy = MakeMiniBtn("📋  คัดลอกรายงาน", 16, 390, 150, 34);
			btnCopy.BorderColor = colAccent;
			btnCopy.Click += delegate
			{
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(summary);
				sb.AppendLine("---");
				foreach (object it in lb.Items)
				{
					sb.AppendLine(it.ToString());
				}
				Clipboard.SetText(sb.ToString());
				ToastOk("คัดลอกรายงานการสแกนไปยัง Clipboard แล้ว");
			};
			dlg.Controls.Add(btnCopy);

			RoundedBtn btnClose = MakeMiniBtn("ปิด", 524, 390, 100, 34);
			btnClose.BorderColor = colField;
			btnClose.Click += delegate { dlg.Close(); };
			dlg.Controls.Add(btnClose);
			dlg.CancelButton = btnClose;
			DpiHelper.ScaleControlTree(dlg);

			dlg.ShowDialog(this);
		}
	}

	private void CheckPostGameCrash()
	{
		try
		{
			GuardRuntimeStatus rt = core.GetGuardStatus(GetGameDir());
			if (rt != null && rt.HasCrash && rt.Crash != null)
			{
				CyberAudio.PlayAlert();
				ShowCrashReportDialog(rt.Crash);
			}
		}
		catch
		{
		}
	}

	private void ShowLatestCrashDialog(bool notifyIfNone)
	{
		string gameDir = GetGameDir();
		GuardRuntimeStatus rt = core.GetGuardStatus(gameDir);
		if (rt == null || !rt.HasCrash || rt.Crash == null)
		{
			if (notifyIfNone)
			{
				Toast("ไม่พบรายงานแครชล่าสุด (_mod_backup\\crash_latest.json)");
			}
			return;
		}
		ShowCrashReportDialog(rt.Crash);
	}

	private void ShowCrashReportDialog(CrashInfo c)
	{
		if (c == null)
		{
			return;
		}
		string gameDir = GetGameDir();
		using (Form dlg = new Form())
		{
			dlg.Text = "⚠️ Sonic Guard — วิเคราะห์สาเหตุเกมเด้ง (Crash Analyzer)";
			dlg.ClientSize = DpiHelper.Sz(560, 400);
			dlg.StartPosition = FormStartPosition.CenterParent;
			dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
			dlg.MaximizeBox = false;
			dlg.MinimizeBox = false;
			dlg.BackColor = colBg;
			dlg.ForeColor = colText;

			AddLbl(dlg, "ตรวจพบ gta_sa.exe ปิดตัวผิดปกติ", fTitle, 16, 14, Color.FromArgb(255, 99, 99));

			string meta = string.Format(
				"รหัสข้อผิดพลาด: {0} ({1})\r\n" +
				"โมดูลที่เกิดปัญหา: {2} ({3})  •  Address: {4}\r\n" +
				"RAM ขณะเกิดเหตุ: Working {5} MB / Virtual {6} MB",
				c.ExceptionCode, c.ExceptionName,
				string.IsNullOrEmpty(c.ModuleName) ? "unknown" : c.ModuleName,
				c.ModuleOffset, c.FaultAddress,
				c.WorkingSetMb, c.VirtualMemMb);
			Label lblMeta = AddLbl(dlg, meta, fSmall, 16, 46, colSub);
			lblMeta.AutoSize = false;
			lblMeta.Size = new Size(528, 54);

			RichTextBox rtb = new RichTextBox();
			rtb.Location = new Point(16, 108);
			rtb.Size = new Size(528, 226);
			rtb.BackColor = colCard;
			rtb.ForeColor = colText;
			rtb.BorderStyle = BorderStyle.FixedSingle;
			rtb.Font = fField;
			rtb.ReadOnly = true;

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("📌 สาเหตุที่คาดว่าเป็นไปได้:");
			sb.AppendLine(c.ThaiExplanation);
			sb.AppendLine();
			sb.AppendLine("💡 คำแนะนำการแก้ไข:");
			sb.AppendLine(c.Suggestion);
			if (c.LoadedModules != null && c.LoadedModules.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine("🧩 ปลั๊กอินที่โหลดอยู่ขณะเกิดเหตุ:");
				sb.AppendLine(string.Join(", ", c.LoadedModules.ToArray()));
			}
			rtb.Text = sb.ToString();
			dlg.Controls.Add(rtb);

			RoundedBtn btnScan = MakeMiniBtn("⚡ สแกนหาม็อดเสีย", 16, 348, 164, 34);
			btnScan.BorderColor = colAccent;
			btnScan.Click += delegate
			{
				dlg.Close();
				ShowPage(6);
				RunAssetScanDialog();
			};
			dlg.Controls.Add(btnScan);

			RoundedBtn btnClear = MakeMiniBtn("🗑️ ล้างประวัติแครชนี้", 190, 348, 174, 34);
			btnClear.BorderColor = Color.FromArgb(243, 156, 18);
			btnClear.Click += delegate
			{
				core.ClearLatestCrash(gameDir);
				RefreshModsPage();
				ToastOk("ล้างรายงานแครชล่าสุดแล้ว");
				dlg.Close();
			};
			dlg.Controls.Add(btnClear);

			RoundedBtn btnClose = MakeMiniBtn("ปิด", 444, 348, 100, 34);
			btnClose.BorderColor = colField;
			btnClose.Click += delegate { dlg.Close(); };
			dlg.Controls.Add(btnClose);
			dlg.CancelButton = btnClose;
			DpiHelper.ScaleControlTree(dlg);

			dlg.ShowDialog(this);
		}
	}
}
