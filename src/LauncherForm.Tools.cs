using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal partial class LauncherForm
{
	private int chatLoadGen;
	private System.Windows.Forms.Timer chatSearchTimer;
	private RoundedBtn btnGalRefresh;
	private RoundedBtn btnGalFolder;
	private RoundedBtn btnChatReload;
	private RoundedBtn btnChatCopy;
	private RoundedBtn btnChatOpen;
	private SmoothPanel pnlChatBox;
	private Label lblRconPort;
	private Label lblRconPass;
	private Label lblRconRes;
	private RoundedBtn btnRconTest;
	private RoundedBtn btnRconVars;
	private RoundedBtn btnRconGmx;
	private RoundedBtn btnRconSend;
	private Dictionary<Control, Rectangle> modsBaseBounds;
	private int modsCard0BaseW;
	private int modsCard0BaseH;
	private int modsCard1BaseW;
	private int modsCard1BaseH;

	private void BuildGalPage()
	{
		AddLbl(pnlGal, "แกลเลอรีสกรีนช็อต (F8 ในเกม)", fTitle, 0, 0, colText);
		galCount = AddLbl(pnlGal, "", fSub, 0, 32, colSub);
		btnGalRefresh = MakeMiniBtn("↻  รีเฟรช", 446, 22, 106, 34);
		btnGalRefresh.Click += delegate
		{
			LoadGallery();
		};
		btnGalFolder = MakeMiniBtn("📂  เปิดโฟลเดอร์", 558, 22, 110, 34);
		btnGalFolder.Click += delegate
		{
			string text = ShotDir();
			if (!Directory.Exists(text))
			{
				try
				{
					Directory.CreateDirectory(text);
				}
				catch { }
			}
			if (Directory.Exists(text))
			{
				Process.Start("explorer.exe", text);
			}
			else
			{
				ShowErr("ไม่สามารถเปิดโฟลเดอร์สกรีนช็อตได้");
			}
		};
		pnlGal.Controls.Add(btnGalRefresh);
		pnlGal.Controls.Add(btnGalFolder);
		galFlow = new FlowLayoutPanel();
		galFlow.Location = new Point(0, 64);
		galFlow.Size = new Size(668, 364);
		galFlow.BackColor = colCard;
		galFlow.AutoScroll = true;
		galFlow.Padding = new Padding(8);
		EnableDoubleBuffer(galFlow);
		pnlGal.Controls.Add(galFlow);
	}

	private void BuildChatPage()
	{
		txtChatSearch = AddField(pnlChat, "🔍  พิมพ์คำค้นหา (ชื่อผู้เล่น, คำสั่ง, ประวัติการซื้อขาย, แชท)...", 0, 0, 370);
		chatSearchTimer = TrackDisposable(new System.Windows.Forms.Timer());
		chatSearchTimer.Interval = 150;
		chatSearchTimer.Tick += delegate
		{
			chatSearchTimer.Stop();
			LoadChatLines();
		};
		txtChatSearch.TextChanged += delegate
		{
			if (chatSearchTimer != null)
			{
				chatSearchTimer.Stop();
				chatSearchTimer.Start();
			}
		};

		btnChatReload = MakeMiniBtn("↻  รีเฟรช", 380, 0, 90, 42);
		btnChatReload.BorderColor = colAccent;
		btnChatReload.Click += delegate
		{
			LoadChatLines();
		};
		pnlChat.Controls.Add(btnChatReload);

		btnChatCopy = MakeMiniBtn("📋  คัดลอก", 478, 0, 96, 42);
		btnChatCopy.BorderColor = colYellow;
		btnChatCopy.Click += delegate
		{
			if (txtChatLog != null && !string.IsNullOrEmpty(txtChatLog.Text))
			{
				try
				{
					Clipboard.SetText(txtChatLog.Text);
					ToastOk("คัดลอกบันทึกแชทแล้ว");
				}
				catch { }
			}
		};
		pnlChat.Controls.Add(btnChatCopy);

		btnChatOpen = MakeMiniBtn("📂  โฟลเดอร์", 582, 0, 86, 42);
		btnChatOpen.BorderColor = colAccent;
		btnChatOpen.Click += delegate
		{
			try
			{
				string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP");
				if (Directory.Exists(path))
				{
					Process.Start("explorer.exe", path);
				}
				else
				{
					ShowErr("ยังไม่พบโฟลเดอร์ SAMP ใน Documents");
				}
			}
			catch { }
		};
		pnlChat.Controls.Add(btnChatOpen);

		lblChatInfo = AddLbl(pnlChat, "กำลังโหลดบันทึกแชท...", fSmall, 2, 46, colSub);

		pnlChatBox = new SmoothPanel(colField, 10);
		pnlChatBox.Location = new Point(0, 68);
		pnlChatBox.Size = new Size(668, 360);
		pnlChatBox.Padding = new Padding(10);
		pnlChatBox.BorderColor = Color.FromArgb(40, 60, 95);

		txtChatLog = new RichTextBox();
		txtChatLog.Dock = DockStyle.Fill;
		txtChatLog.BorderStyle = BorderStyle.None;
		txtChatLog.BackColor = colField;
		txtChatLog.ForeColor = Color.FromArgb(235, 245, 255);
		txtChatLog.Font = GetCachedFont("Leelawadee UI", 9.75f, FontStyle.Regular);
		txtChatLog.ReadOnly = true;
		txtChatLog.WordWrap = false;
		txtChatLog.ScrollBars = RichTextBoxScrollBars.Both;

		pnlChatBox.Controls.Add(txtChatLog);
		pnlChat.Controls.Add(pnlChatBox);
	}

	private void LoadChatLines()
	{
		chatLoadGen++;
		int gen = chatLoadGen;
		string query = FieldVal(txtChatSearch).Trim();
		string chatlogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP", "chatlog.txt");
		if (lblChatInfo != null)
		{
			lblChatInfo.Text = string.IsNullOrEmpty(query) ? "กำลังโหลดบันทึกแชทล่าสุด..." : "กำลังค้นหาในบันทึกแชท...";
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				ChatlogResultDto res = core.ReadChatlog(chatlogPath, 2 * 1024 * 1024, 5000, query);
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						if (gen != chatLoadGen || base.IsDisposed || txtChatLog == null || lblChatInfo == null)
						{
							return;
						}
						if (res == null || !res.Exists)
						{
							lblChatInfo.Text = "ยังไม่พบไฟล์ chatlog.txt (ไฟล์จะถูกสร้างอัตโนมัติเมื่อเข้าเล่นในเซิร์ฟเวอร์)";
							txtChatLog.Text = "--- ยังไม่มีประวัติแชท ---\nเข้าเล่นในเซิร์ฟเวอร์ SA-MP เพื่อเริ่มเก็บบันทึกข้อความสนทนา";
							return;
						}
						txtChatLog.Text = (res.Lines != null && res.Lines.Count > 0)
							? string.Join("\r\n", res.Lines.ToArray()) + "\r\n"
							: "";
						string note = res.Truncated ? " (แสดงสูงสุด 5,000 บรรทัดล่าสุด)" : "";
						if (!string.IsNullOrEmpty(query))
						{
							lblChatInfo.Text = string.Format("ผลการค้นหา: พบ {0} จาก {1} บรรทัด{2}", res.MatchedCount, res.TotalLines, note);
						}
						else
						{
							lblChatInfo.Text = string.Format("แสดงประวัติแชท {0} บรรทัด{1}", res.TotalLines, note);
						}
						txtChatLog.SelectionStart = txtChatLog.Text.Length;
						txtChatLog.ScrollToCaret();
					});
				}
			}
			catch (Exception ex)
			{
				try
				{
					if (!base.IsDisposed)
					{
						BeginInvoke((MethodInvoker)delegate
						{
							if (gen == chatLoadGen && lblChatInfo != null)
							{
								lblChatInfo.Text = "เกิดข้อผิดพลาดในการอ่านไฟล์: " + ex.Message;
							}
						});
					}
				}
				catch
				{
				}
			}
		});
	}

	private string ShotDir()
	{
		string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		string sampScreens = Path.Combine(myDocs, "GTA San Andreas User Files", "SAMP", "screens");
		if (Directory.Exists(sampScreens))
		{
			return sampScreens;
		}
		string altScreens = Path.Combine(myDocs, "GTA San Andreas User Files", "Screenshots");
		if (Directory.Exists(altScreens))
		{
			return altScreens;
		}
		string gallery = Path.Combine(myDocs, "GTA San Andreas User Files", "Gallery");
		if (Directory.Exists(gallery))
		{
			return gallery;
		}
		return sampScreens;
	}

	private void LoadGallery()
	{
		string path = ShotDir();
		List<Image> list = new List<Image>();
		galFlow.SuspendLayout();
		try
		{
			while (galFlow.Controls.Count > 0)
			{
				Control control = galFlow.Controls[0];
				PictureBox pictureBox = control as PictureBox;
				if (pictureBox != null && pictureBox.Image != null)
				{
					list.Add(pictureBox.Image);
					pictureBox.Image = null;
				}
				galFlow.Controls.RemoveAt(0);
				try
				{
					if (control.Region != null)
					{
						Region oldReg = control.Region;
						control.Region = null;
						oldReg.Dispose();
					}
					control.Dispose();
				}
				catch { }
			}
		}
		finally
		{
			galFlow.ResumeLayout();
		}
		foreach (Image item in list)
		{
			try { item.Dispose(); } catch { }
		}
		galGen++;
		int gen = galGen;
		currentGalleryPaths.Clear();
		galCount.Text = "กำลังโหลด...";
		ThreadPool.QueueUserWorkItem(delegate
		{
			GalleryScanResultDto gres = core.ScanGallery(path, 150);
			if (base.IsDisposed || gen != galGen)
			{
				return;
			}
			List<string> pathsSnapshot = new List<string>();
			if (gres != null && gres.Items != null)
			{
				foreach (GalleryItemDto item2 in gres.Items)
				{
					if (item2 != null && !string.IsNullOrEmpty(item2.Path))
					{
						pathsSnapshot.Add(item2.Path);
					}
				}
			}
			int totalFound = (gres != null) ? gres.TotalFound : pathsSnapshot.Count;
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						if (galFlow.IsDisposed || gen != galGen) return;
						currentGalleryPaths.Clear();
						currentGalleryPaths.AddRange(pathsSnapshot);
						galCount.Text = (totalFound > pathsSnapshot.Count)
							? (totalFound + " รูป (แสดง " + pathsSnapshot.Count + " ล่าสุด)")
							: (pathsSnapshot.Count + " รูป");
						if (pathsSnapshot.Count == 0)
						{
							Label label = new Label();
							label.Text = "ยังไม่มีสกรีนช็อต — เข้าเกมแล้วกด F8 ถ่ายรูป แล้วรีเฟรชที่นี่";
							label.ForeColor = colSub;
							label.Font = fSub;
							label.AutoSize = true;
							label.Padding = new Padding(16, 20, 8, 8);
							galFlow.Controls.Add(label);
						}
					});
				}
			}
			catch { }
			if (pathsSnapshot.Count == 0)
			{
				return;
			}
			List<KeyValuePair<string, Image>> batch = new List<KeyValuePair<string, Image>>(8);
			for (int i = 0; i < pathsSnapshot.Count; i++)
			{
				if (base.IsDisposed || gen != galGen)
				{
					break;
				}
				string path2 = pathsSnapshot[i];
				Image thumb = MakeThumb(path2);
				if (thumb != null)
				{
					batch.Add(new KeyValuePair<string, Image>(path2, thumb));
				}
				if (batch.Count >= 8 || (i == pathsSnapshot.Count - 1 && batch.Count > 0))
				{
					List<KeyValuePair<string, Image>> dispatch = batch;
					batch = new List<KeyValuePair<string, Image>>(8);
					try
					{
						if (!base.IsDisposed)
						{
							BeginInvoke((MethodInvoker)delegate
							{
								if (galFlow.IsDisposed || gen != galGen)
								{
									foreach (KeyValuePair<string, Image> kv in dispatch)
									{
										kv.Value.Dispose();
									}
									return;
								}
								galFlow.SuspendLayout();
								try
								{
									foreach (KeyValuePair<string, Image> kv in dispatch)
									{
										string shotPath = kv.Key;
										PictureBox pictureBox2 = new PictureBox();
										pictureBox2.Image = kv.Value;
										pictureBox2.Size = kv.Value.Size;
										pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
										pictureBox2.BackColor = colField;
										pictureBox2.Padding = new Padding(2);
										pictureBox2.Cursor = Cursors.Hand;
										pictureBox2.Click += delegate
										{
											OpenShotViewer(shotPath);
										};
										galFlow.Controls.Add(pictureBox2);
									}
								}
								finally
								{
									galFlow.ResumeLayout();
								}
							});
						}
						else
						{
							foreach (KeyValuePair<string, Image> kv in dispatch) kv.Value.Dispose();
						}
					}
					catch
					{
						foreach (KeyValuePair<string, Image> kv in dispatch) kv.Value.Dispose();
					}
				}
			}
			foreach (KeyValuePair<string, Image> leftover in batch)
			{
				try { leftover.Value.Dispose(); } catch { }
			}
		});
	}

	private Image MakeThumb(string path)
	{
		try
		{
			using (Image image = Image.FromFile(path))
			{
				int num = DpiHelper.S(200);
				int num2 = DpiHelper.S(112);
				double num3 = Math.Max((double)num / (double)image.Width, (double)num2 / (double)image.Height);
				int num4 = Math.Max(1, (int)((double)num / num3));
				int num5 = Math.Max(1, (int)((double)num2 / num3));
				int srcX = (image.Width - num4) / 2;
				int srcY = (image.Height - num5) / 2;
				Bitmap bitmap = new Bitmap(num, num2);
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.DrawImage(image, new Rectangle(0, 0, num, num2), srcX, srcY, num4, num5, GraphicsUnit.Pixel);
				}
				return bitmap;
			}
		}
		catch
		{
			return null;
		}
	}

	private void OpenShotViewer(string path)
	{
		try
		{
			ViewerForm vf = new ViewerForm(path, currentGalleryPaths);
			vf.FormClosed += delegate { vf.Dispose(); };
			vf.Show(this);
		}
		catch (Exception ex)
		{
			ShowErr("เปิดรูปไม่ได้: " + ex.Message);
		}
	}

	private void BuildRconPage()
	{
		AddLbl(pnlRcon, "RCON Panel — คุมเซิร์ฟเวอร์ของคุณ", fTitle, 0, 0, colText);
		AddLbl(pnlRcon, "IP", fSub, 0, 36, colSub);
		txtRconIp = AddField(pnlRcon, "127.0.0.1", 0, 56, 300);
		lblRconPort = AddLbl(pnlRcon, "PORT", fSub, 312, 36, colSub);
		txtRconPort = AddField(pnlRcon, "7777", 312, 56, 140);
		lblRconPass = AddLbl(pnlRcon, "รหัส RCON", fSub, 464, 36, colSub);
		txtRconPass = AddField(pnlRcon, "", 464, 56, 204);
		txtRconPass.UseSystemPasswordChar = true;
		btnRconTest = MakeMiniBtn("↻  ทดสอบเชื่อมต่อ", 0, 112, 214, 40);
		btnRconTest.Click += delegate
		{
			RconRun("echo rcon_ok", true);
		};
		btnRconVars = MakeMiniBtn("varlist", 226, 112, 214, 40);
		btnRconVars.Click += delegate
		{
			RconRun("varlist", false);
		};
		btnRconGmx = MakeMiniBtn("⚠  gmx (รีสตาร์ทโหมด)", 452, 112, 216, 40);
		btnRconGmx.Click += delegate
		{
			RconRun("gmx", false);
		};
		pnlRcon.Controls.Add(btnRconTest);
		pnlRcon.Controls.Add(btnRconVars);
		pnlRcon.Controls.Add(btnRconGmx);
		lblRconRes = AddLbl(pnlRcon, "ผลลัพธ์", fSub, 0, 162, colSub);
		txtRconOut = new TextBox();
		txtRconOut.Multiline = true;
		txtRconOut.ReadOnly = true;
		txtRconOut.BackColor = Color.FromArgb(9, 12, 18);
		txtRconOut.ForeColor = colOk;
		txtRconOut.Font = GetCachedFont("Consolas", 10f, FontStyle.Regular);
		txtRconOut.BorderStyle = BorderStyle.FixedSingle;
		txtRconOut.Location = new Point(0, 182);
		txtRconOut.Size = new Size(668, 180);
		txtRconOut.ScrollBars = ScrollBars.Vertical;
		pnlRcon.Controls.Add(txtRconOut);
		txtRconCmd = AddField(pnlRcon, "พิมพ์คำสั่ง: say / kick <id> / ban <id> / changemode <name> / gmx...", 0, 374, 552);
		txtRconCmd.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.KeyCode == Keys.Return)
			{
				ev.SuppressKeyPress = true;
				string text = FieldVal(txtRconCmd);
				if (text.Length > 0)
				{
					RconRun(text, false);
					ForceText(txtRconCmd, "");
				}
			}
		};
		btnRconSend = MakeMiniBtn("ส่งคำสั่ง", 560, 374, 108, 42);
		btnRconSend.BorderColor = colAccent;
		btnRconSend.Click += delegate
		{
			string text2 = FieldVal(txtRconCmd);
			if (text2.Length > 0)
			{
				RconRun(text2, false);
				ForceText(txtRconCmd, "");
			}
		};
		pnlRcon.Controls.Add(btnRconSend);
	}

	private void LayoutToolsPages(int pageW, int pageH)
	{
		// Gallery Page
		if (galFlow != null && btnGalRefresh != null && btnGalFolder != null)
		{
			int wFolder = DpiHelper.S(116);
			int wRefresh = DpiHelper.S(106);
			int gGap = DpiHelper.S(8);
			btnGalFolder.Size = new Size(wFolder, DpiHelper.S(34));
			btnGalFolder.Location = new Point(pageW - wFolder, DpiHelper.S(20));
			btnGalRefresh.Size = new Size(wRefresh, DpiHelper.S(34));
			btnGalRefresh.Location = new Point(btnGalFolder.Left - gGap - wRefresh, DpiHelper.S(20));
			galFlow.Location = new Point(0, DpiHelper.S(64));
			galFlow.Size = new Size(pageW, Math.Max(DpiHelper.S(160), pageH - DpiHelper.S(64)));
		}

		// Chat Page
		if (pnlChatBox != null && btnChatReload != null && btnChatCopy != null && btnChatOpen != null)
		{
			int topH = DpiHelper.S(42);
			int cGap = DpiHelper.S(8);
			int wOpen = DpiHelper.S(92);
			int wCopy = DpiHelper.S(96);
			int wReload = DpiHelper.S(94);
			btnChatOpen.Size = new Size(wOpen, topH);
			btnChatOpen.Location = new Point(pageW - wOpen, 0);
			btnChatCopy.Size = new Size(wCopy, topH);
			btnChatCopy.Location = new Point(btnChatOpen.Left - cGap - wCopy, 0);
			btnChatReload.Size = new Size(wReload, topH);
			btnChatReload.Location = new Point(btnChatCopy.Left - cGap - wReload, 0);
			if (txtChatSearch != null && txtChatSearch.Parent != null)
			{
				txtChatSearch.Parent.Location = new Point(0, 0);
				txtChatSearch.Parent.Size = new Size(Math.Max(DpiHelper.S(180), btnChatReload.Left - DpiHelper.S(10)), topH);
			}
			if (lblChatInfo != null)
			{
				lblChatInfo.Location = new Point(DpiHelper.S(2), DpiHelper.S(46));
			}
			pnlChatBox.Location = new Point(0, DpiHelper.S(68));
			pnlChatBox.Size = new Size(pageW, Math.Max(DpiHelper.S(160), pageH - DpiHelper.S(68)));
		}

		// RCON Page
		if (txtRconOut != null && txtRconIp != null && txtRconPort != null && txtRconPass != null)
		{
			int rGap = DpiHelper.S(10);
			int ipW = Math.Max(DpiHelper.S(180), (int)((pageW - rGap * 2) * 0.44));
			int portW = Math.Max(DpiHelper.S(100), (int)((pageW - rGap * 2) * 0.21));
			int passW = Math.Max(DpiHelper.S(140), pageW - ipW - portW - rGap * 2);
			int lblY = DpiHelper.S(36);
			int fieldY = DpiHelper.S(56);
			int fieldH = DpiHelper.S(42);

			if (txtRconIp.Parent != null)
			{
				txtRconIp.Parent.Location = new Point(0, fieldY);
				txtRconIp.Parent.Size = new Size(ipW, fieldH);
			}
			int portX = ipW + rGap;
			if (lblRconPort != null) lblRconPort.Location = new Point(portX, lblY);
			if (txtRconPort.Parent != null)
			{
				txtRconPort.Parent.Location = new Point(portX, fieldY);
				txtRconPort.Parent.Size = new Size(portW, fieldH);
			}
			int passX = portX + portW + rGap;
			if (lblRconPass != null) lblRconPass.Location = new Point(passX, lblY);
			if (txtRconPass.Parent != null)
			{
				txtRconPass.Parent.Location = new Point(passX, fieldY);
				txtRconPass.Parent.Size = new Size(passW, fieldH);
			}

			int btnY = DpiHelper.S(112);
			int btnH = DpiHelper.S(40);
			int btnW = (pageW - rGap * 2) / 3;
			if (btnRconTest != null) { btnRconTest.Location = new Point(0, btnY); btnRconTest.Size = new Size(btnW, btnH); }
			if (btnRconVars != null) { btnRconVars.Location = new Point(btnW + rGap, btnY); btnRconVars.Size = new Size(btnW, btnH); }
			if (btnRconGmx != null) { btnRconGmx.Location = new Point((btnW + rGap) * 2, btnY); btnRconGmx.Size = new Size(pageW - (btnW + rGap) * 2, btnH); }

			if (lblRconRes != null) lblRconRes.Location = new Point(0, DpiHelper.S(162));
			int cmdH = DpiHelper.S(42);
			int cmdY = pageH - cmdH;
			int outY = DpiHelper.S(182);
			txtRconOut.Location = new Point(0, outY);
			txtRconOut.Size = new Size(pageW, Math.Max(DpiHelper.S(120), cmdY - outY - DpiHelper.S(10)));

			int sendW = DpiHelper.S(112);
			if (txtRconCmd != null && txtRconCmd.Parent != null)
			{
				txtRconCmd.Parent.Location = new Point(0, cmdY);
				txtRconCmd.Parent.Size = new Size(Math.Max(DpiHelper.S(160), pageW - sendW - DpiHelper.S(8)), cmdH);
			}
			if (btnRconSend != null)
			{
				btnRconSend.Location = new Point(pageW - sendW, cmdY);
				btnRconSend.Size = new Size(sendW, cmdH);
			}
		}

		// Settings Page
		LayoutSettingsPage(pageW, pageH);
	}

	private void LayoutModsPage(int pageW, int pageH)
	{
		if (pnlMods == null || pnlMods.Controls.Count < 2) return;
		Control card0 = pnlMods.Controls[0];
		Control card1 = pnlMods.Controls[1];

		if (modsBaseBounds == null)
		{
			modsBaseBounds = new Dictionary<Control, Rectangle>();
			modsCard0BaseW = Math.Max(1, card0.Width);
			modsCard0BaseH = Math.Max(1, card0.Height);
			modsCard1BaseW = Math.Max(1, card1.Width);
			modsCard1BaseH = Math.Max(1, card1.Height);
			SnapshotBoundsTree(card0);
			SnapshotBoundsTree(card1);
		}

		int gap = DpiHelper.S(14);
		int leftW = (pageW - gap) / 2;
		int rightW = pageW - leftW - gap;

		card0.Location = new Point(0, 0);
		card0.Size = new Size(leftW, pageH);
		LayoutModsCard(card0, modsCard0BaseW, modsCard0BaseH, leftW, pageH);

		card1.Location = new Point(leftW + gap, 0);
		card1.Size = new Size(rightW, pageH);
		LayoutModsCard(card1, modsCard1BaseW, modsCard1BaseH, rightW, pageH);
	}

	private void SnapshotBoundsTree(Control parent)
	{
		foreach (Control c in parent.Controls)
		{
			modsBaseBounds[c] = c.Bounds;
			if (c is SmoothPanel)
			{
				SnapshotBoundsTree(c);
			}
		}
	}

	private void LayoutModsCard(Control card, int baseW, int baseH, int newW, int newH)
	{
		int dh = newH - baseH;
		int origClbBottom = -1;
		Rectangle clbOrig;
		if (clbModFolders != null && clbModFolders.Parent == card && modsBaseBounds.TryGetValue(clbModFolders, out clbOrig))
		{
			origClbBottom = clbOrig.Bottom;
		}
		int midX = newW / 2;
		int halfGap = DpiHelper.S(4);

		foreach (Control c in card.Controls)
		{
			Rectangle r;
			if (!modsBaseBounds.TryGetValue(c, out r)) continue;
			int top = r.Top;
			if (origClbBottom > 0 && r.Top >= origClbBottom - DpiHelper.S(2))
			{
				top = r.Top + dh;
			}
			if (c == clbModFolders)
			{
				int rightMargin = baseW - r.Right;
				c.Location = new Point(r.Left, top);
				c.Size = new Size(Math.Max(DpiHelper.S(120), newW - r.Left - rightMargin), Math.Max(DpiHelper.S(80), r.Height + dh));
			}
			else if (r.Width >= (int)(baseW * 0.65f) && !(c is Label && c.AutoSize))
			{
				int rightMargin = baseW - r.Right;
				c.Location = new Point(r.Left, top);
				c.Size = new Size(Math.Max(DpiHelper.S(100), newW - r.Left - rightMargin), r.Height);
				if (c is SmoothPanel)
				{
					LayoutModsCard(c, r.Width, r.Height, c.Width, c.Height);
				}
			}
			else if (r.Left >= (int)(baseW * 0.68f) && r.Width < (int)(baseW * 0.34f))
			{
				int rightOffset = baseW - r.Left;
				c.Location = new Point(Math.Max(DpiHelper.S(12), newW - rightOffset), top);
			}
			else if (r.Left <= (int)(baseW * 0.15f) && r.Width >= (int)(baseW * 0.35f) && r.Width < (int)(baseW * 0.60f))
			{
				c.Location = new Point(r.Left, top);
				c.Size = new Size(Math.Max(DpiHelper.S(80), midX - halfGap - r.Left), r.Height);
			}
			else if (r.Left >= (int)(baseW * 0.45f) && r.Left < (int)(baseW * 0.65f) && r.Width >= (int)(baseW * 0.35f) && r.Width < (int)(baseW * 0.60f))
			{
				int rightMargin = Math.Max(DpiHelper.S(12), baseW - r.Right);
				int nx = midX + halfGap;
				c.Location = new Point(nx, top);
				c.Size = new Size(Math.Max(DpiHelper.S(80), newW - rightMargin - nx), r.Height);
			}
			else
			{
				c.Location = new Point(r.Left, top);
			}
		}
	}

	private void RconRun(string cmd, bool isTest)
	{
		if (cmd == null || cmd.Trim().Length == 0)
		{
			return;
		}
		string ip = FieldVal(txtRconIp);
		string s = FieldVal(txtRconPort);
		string pass = FieldVal(txtRconPass);
		int port;
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(s, out port) || port < 1 || port > 65535)
		{
			ShowErr("IP / Port ไม่ถูกต้อง");
			return;
		}
		SaveConfig();
		AppendRcon("> " + cmd);
		string c = cmd.Trim();
		ThreadPool.QueueUserWorkItem(delegate
		{
			string res = core.RconSend(ip, port, pass, c);
			if (res.Length == 0)
			{
				res = "(ไม่มีข้อความตอบกลับ)";
			}
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						AppendRcon(res);
						if (isTest)
						{
							SetStatus((res.Trim() == "rcon_ok") ? "RCON เชื่อมต่อสำเร็จ ✓" : ("RCON: " + res.Split('\n')[0]));
						}
					});
				}
			}
			catch
			{
			}
		});
	}

	private void AppendRcon(string s)
	{
		txtRconOut.AppendText(s + Environment.NewLine);
	}
}
