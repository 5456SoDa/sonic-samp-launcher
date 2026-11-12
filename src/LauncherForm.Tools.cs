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
	private void BuildGalPage()
	{
		AddLbl(pnlGal, "แกลเลอรีสกรีนช็อต (F8 ในเกม)", fTitle, 0, 0, colText);
		galCount = AddLbl(pnlGal, "", fSub, 0, 32, colSub);
		RoundedBtn roundedBtn = MakeMiniBtn("↻  รีเฟรช", 446, 22, 106, 34);
		roundedBtn.Click += delegate
		{
			LoadGallery();
		};
		RoundedBtn roundedBtn2 = MakeMiniBtn("📂  เปิดโฟลเดอร์", 558, 22, 110, 34);
		roundedBtn2.Click += delegate
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
		pnlGal.Controls.Add(roundedBtn);
		pnlGal.Controls.Add(roundedBtn2);
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
		txtChatSearch.TextChanged += delegate
		{
			FilterChatLines();
		};

		RoundedBtn btnReload = MakeMiniBtn("↻  รีเฟรช", 380, 0, 90, 42);
		btnReload.BorderColor = colAccent;
		btnReload.Click += delegate
		{
			LoadChatLines();
		};
		pnlChat.Controls.Add(btnReload);

		RoundedBtn btnCopy = MakeMiniBtn("📋  คัดลอก", 478, 0, 96, 42);
		btnCopy.BorderColor = colYellow;
		btnCopy.Click += delegate
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
		pnlChat.Controls.Add(btnCopy);

		RoundedBtn btnOpen = MakeMiniBtn("📂  โฟลเดอร์", 582, 0, 86, 42);
		btnOpen.BorderColor = colAccent;
		btnOpen.Click += delegate
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
		pnlChat.Controls.Add(btnOpen);

		lblChatInfo = AddLbl(pnlChat, "กำลังโหลดบันทึกแชท...", fSmall, 2, 46, colSub);

		SmoothPanel logBox = new SmoothPanel(colField, 10);
		logBox.Location = new Point(0, 68);
		logBox.Size = new Size(668, 360);
		logBox.Padding = new Padding(10);
		logBox.BorderColor = Color.FromArgb(40, 60, 95);

		txtChatLog = new RichTextBox();
		txtChatLog.Dock = DockStyle.Fill;
		txtChatLog.BorderStyle = BorderStyle.None;
		txtChatLog.BackColor = colField;
		txtChatLog.ForeColor = Color.FromArgb(235, 245, 255);
		txtChatLog.Font = new Font("Leelawadee UI", 9.75f);
		txtChatLog.ReadOnly = true;
		txtChatLog.WordWrap = false;
		txtChatLog.ScrollBars = RichTextBoxScrollBars.Both;

		logBox.Controls.Add(txtChatLog);
		pnlChat.Controls.Add(logBox);
	}

	private void LoadChatLines()
	{
		chatLines.Clear();
		string chatlogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP", "chatlog.txt");
		if (!File.Exists(chatlogPath))
		{
			if (lblChatInfo != null)
			{
				lblChatInfo.Text = "ยังไม่พบไฟล์ chatlog.txt (ไฟล์จะถูกสร้างอัตโนมัติเมื่อเข้าเล่นในเซิร์ฟเวอร์)";
			}
			if (txtChatLog != null)
			{
				txtChatLog.Text = "--- ยังไม่มีประวัติแชท ---\nเข้าเล่นในเซิร์ฟเวอร์ SA-MP เพื่อเริ่มเก็บบันทึกข้อความสนทนา";
			}
			return;
		}

		try
		{
			using (FileStream fs = new FileStream(chatlogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				byte[] fileBytes = new byte[fs.Length];
				fs.Read(fileBytes, 0, fileBytes.Length);

				string content;
				if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
				{
					content = Encoding.UTF8.GetString(fileBytes, 3, fileBytes.Length - 3);
				}
				else
				{
					bool isUtf8 = true;
					try
					{
						UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
						content = strictUtf8.GetString(fileBytes);
					}
					catch
					{
						isUtf8 = false;
						content = "";
					}
					if (!isUtf8)
					{
						content = Encoding.GetEncoding(874).GetString(fileBytes);
					}
				}

				using (StringReader sr = new StringReader(content))
				{
					string line;
					while ((line = sr.ReadLine()) != null)
					{
						chatLines.Add(line);
					}
				}
			}
			FilterChatLines();
		}
		catch (Exception ex)
		{
			if (lblChatInfo != null)
			{
				lblChatInfo.Text = "เกิดข้อผิดพลาดในการอ่านไฟล์: " + ex.Message;
			}
		}
	}

	private void FilterChatLines()
	{
		if (txtChatLog == null || lblChatInfo == null)
		{
			return;
		}
		string query = FieldVal(txtChatSearch).Trim();
		StringBuilder sb = new StringBuilder();
		int matchCount = 0;

		for (int i = 0; i < chatLines.Count; i++)
		{
			string line = chatLines[i];
			if (string.IsNullOrEmpty(query) || line.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				sb.AppendLine(line);
				matchCount++;
			}
		}

		txtChatLog.Text = sb.ToString();
		if (!string.IsNullOrEmpty(query))
		{
			lblChatInfo.Text = string.Format("ผลการค้นหา: พบ {0} จากทั้งหมด {1} บรรทัด", matchCount, chatLines.Count);
		}
		else
		{
			lblChatInfo.Text = string.Format("แสดงประวัติแชททั้งหมด {0} บรรทัด (เลื่อนลงเพื่อดูข้อความล่าสุด)", chatLines.Count);
		}

		txtChatLog.SelectionStart = txtChatLog.Text.Length;
		txtChatLog.ScrollToCaret();
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
		foreach (Control control in galFlow.Controls)
		{
			PictureBox pictureBox = control as PictureBox;
			if (pictureBox != null && pictureBox.Image != null)
			{
				list.Add(pictureBox.Image);
			}
		}
		galFlow.Controls.Clear();
		foreach (Image item in list)
		{
			try
			{
				item.Dispose();
			}
			catch
			{
			}
		}
		galGen++;
		int gen = galGen;
		int num = 0;
		if (Directory.Exists(path))
		{
			List<FileInfo> list2 = new List<FileInfo>();
			string[] files = Directory.GetFiles(path);
			foreach (string text in files)
			{
				switch (Path.GetExtension(text).ToLowerInvariant())
				{
				case ".png":
				case ".jpg":
				case ".bmp":
					list2.Add(new FileInfo(text));
					break;
				}
			}
			list2.Sort((FileInfo a, FileInfo b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
			if (list2.Count > 150)
			{
				list2.RemoveRange(150, list2.Count - 150);
			}
			currentGalleryPaths.Clear();
			foreach (FileInfo item2 in list2)
			{
				currentGalleryPaths.Add(item2.FullName);
			}
			num = list2.Count;
			foreach (FileInfo item2 in list2)
			{
				string path2 = item2.FullName;
				ThreadPool.QueueUserWorkItem(delegate
				{
					Image thumb = MakeThumb(path2);
					if (thumb == null || base.IsDisposed)
					{
						if (thumb != null)
						{
							thumb.Dispose();
						}
						return;
					}
					try
					{
						BeginInvoke((MethodInvoker)delegate
						{
							if (galFlow.IsDisposed || gen != galGen)
							{
								thumb.Dispose();
							}
							else
							{
								PictureBox pictureBox2 = new PictureBox();
								pictureBox2.Image = thumb;
								pictureBox2.Size = thumb.Size;
								pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
								pictureBox2.BackColor = colField;
								pictureBox2.Padding = new Padding(2);
								pictureBox2.Cursor = Cursors.Hand;
								pictureBox2.Click += delegate
								{
									OpenShotViewer(path2);
								};
								galFlow.Controls.Add(pictureBox2);
							}
						});
					}
					catch
					{
						thumb.Dispose();
					}
				});
			}
		}
		if (num == 0)
		{
			Label label = new Label();
			label.Text = "ยังไม่มีสกรีนช็อต — เข้าเกมแล้วกด F8 ถ่ายรูป แล้วรีเฟรชที่นี่";
			label.ForeColor = colSub;
			label.Font = fSub;
			label.AutoSize = true;
			label.Padding = new Padding(16, 20, 8, 8);
			galFlow.Controls.Add(label);
		}
		galCount.Text = num + " รูป";
	}

	private Image MakeThumb(string path)
	{
		try
		{
			using (Image image = Image.FromFile(path))
			{
				int num = 200;
				int num2 = 112;
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
			new ViewerForm(path, currentGalleryPaths).Show(this);
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
		AddLbl(pnlRcon, "PORT", fSub, 312, 36, colSub);
		txtRconPort = AddField(pnlRcon, "7777", 312, 56, 140);
		AddLbl(pnlRcon, "รหัส RCON", fSub, 464, 36, colSub);
		txtRconPass = AddField(pnlRcon, "", 464, 56, 204);
		txtRconPass.UseSystemPasswordChar = true;
		RoundedBtn roundedBtn = MakeMiniBtn("↻  ทดสอบเชื่อมต่อ", 0, 112, 214, 40);
		roundedBtn.Click += delegate
		{
			RconRun("echo rcon_ok", true);
		};
		RoundedBtn roundedBtn2 = MakeMiniBtn("varlist", 226, 112, 214, 40);
		roundedBtn2.Click += delegate
		{
			RconRun("varlist", false);
		};
		RoundedBtn roundedBtn3 = MakeMiniBtn("⚠  gmx (รีสตาร์ทโหมด)", 452, 112, 216, 40);
		roundedBtn3.Click += delegate
		{
			RconRun("gmx", false);
		};
		pnlRcon.Controls.Add(roundedBtn);
		pnlRcon.Controls.Add(roundedBtn2);
		pnlRcon.Controls.Add(roundedBtn3);
		AddLbl(pnlRcon, "ผลลัพธ์", fSub, 0, 162, colSub);
		txtRconOut = new TextBox();
		txtRconOut.Multiline = true;
		txtRconOut.ReadOnly = true;
		txtRconOut.BackColor = Color.FromArgb(9, 12, 18);
		txtRconOut.ForeColor = colOk;
		txtRconOut.Font = new Font("Consolas", 10f);
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
		RoundedBtn roundedBtn4 = MakeMiniBtn("ส่งคำสั่ง", 560, 374, 108, 42);
		roundedBtn4.Click += delegate
		{
			string text2 = FieldVal(txtRconCmd);
			if (text2.Length > 0)
			{
				RconRun(text2, false);
				ForceText(txtRconCmd, "");
			}
		};
		pnlRcon.Controls.Add(roundedBtn4);
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
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(s, out port))
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
		});
	}

	private void AppendRcon(string s)
	{
		txtRconOut.AppendText(s + Environment.NewLine);
	}
}
