using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal partial class LauncherForm
{
	private void BuildPlayPage()
	{
		// 1. Hero Server Card (Y=0, H=140, W=668)
		SmoothPanel smoothPanel = new SmoothPanel(colCard, 12);
		smoothPanel.BorderColor = Color.FromArgb(45, 68, 105);
		smoothPanel.Location = new Point(0, 0);
		smoothPanel.Size = new Size(668, 140);
		pnlPlay.Controls.Add(smoothPanel);

		lblHeroName = new Label();
		lblHeroName.Text = "กรอก IP เพื่อตรวจสถานะเซิร์ฟเวอร์";
		lblHeroName.Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold);
		lblHeroName.ForeColor = Color.White;
		lblHeroName.BackColor = Color.Transparent;
		lblHeroName.Location = new Point(20, 14);
		lblHeroName.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroName);

		lblHeroIp = new Label();
		lblHeroIp.Text = "—";
		lblHeroIp.Font = new Font("Segoe UI Semibold", 9.5f);
		lblHeroIp.ForeColor = colAccent;
		lblHeroIp.BackColor = Color.Transparent;
		lblHeroIp.Location = new Point(20, 44);
		lblHeroIp.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroIp);

		lblHeroMode = new Label();
		lblHeroMode.Text = "";
		lblHeroMode.Font = new Font("Segoe UI Semibold", 9f);
		lblHeroMode.ForeColor = colSub;
		lblHeroMode.BackColor = Color.Transparent;
		lblHeroMode.Location = new Point(20, 68);
		lblHeroMode.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroMode);

		lblHeroPing = new Label();
		lblHeroPing.Text = "";
		lblHeroPing.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		lblHeroPing.ForeColor = colOk;
		lblHeroPing.BackColor = Color.Transparent;
		lblHeroPing.Location = new Point(20, 92);
		lblHeroPing.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroPing);

		Label label = new Label();
		label.Text = "* ปิด Frame Limiter ในเกม เพื่อเล่นได้ 144 FPS อย่างลื่นไหล";
		label.Font = fSmall;
		label.ForeColor = colSub;
		label.BackColor = Color.Transparent;
		label.Location = new Point(20, 114);
		label.AutoSize = true;
		smoothPanel.Controls.Add(label);

		ring = new RingGauge();
		ring.Location = new Point(552, 20);
		ring.Size = new Size(100, 100);
		ring.RingColor = colAccent;
		smoothPanel.Controls.Add(ring);

		RoundedBtn roundedBtn = MakeMiniBtn("↻", 508, 48, 36, 36);
		roundedBtn.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
		roundedBtn.BorderColor = colAccent;
		roundedBtn.Click += delegate
		{
			QueryPlayServer();
		};
		smoothPanel.Controls.Add(roundedBtn);

		// 2. Middle: Symmetrical 2-Column Boxes (Y=148, H=124)
		// Left Box: Player Identity (W=326, H=124)
		SmoothPanel pnlNickBox = new SmoothPanel(colCard, 10);
		pnlNickBox.Location = new Point(0, 148);
		pnlNickBox.Size = new Size(326, 124);
		pnlNickBox.BorderColor = Color.FromArgb(40, 60, 95);
		pnlPlay.Controls.Add(pnlNickBox);

		AddLbl(pnlNickBox, "👤  ข้อมูลตัวละคร (PLAYER IDENTITY)", fSub, 14, 10, colAccent);
		txtNick = AddField(pnlNickBox, "ชื่อเล่น: MyNick_TH", 14, 32, 298);

		RoundedBtn btnProfiles = MakeMiniBtn("👤  โปรไฟล์ชื่อ", 14, 78, 144, 34);
		btnProfiles.BorderColor = colAccent;
		btnProfiles.Click += delegate
		{
			ShowNickProfilesMenu(btnProfiles);
		};
		pnlNickBox.Controls.Add(btnProfiles);

		RoundedBtn btnRand = MakeMiniBtn("🎲  สุ่มชื่อ", 168, 78, 144, 34);
		btnRand.BorderColor = Color.FromArgb(40, 60, 95);
		btnRand.Click += delegate
		{
			ForceText(txtNick, RandomNick());
		};
		pnlNickBox.Controls.Add(btnRand);

		// Right Box: Target Server (W=326, H=124)
		SmoothPanel pnlSrvBox = new SmoothPanel(colCard, 10);
		pnlSrvBox.Location = new Point(342, 148);
		pnlSrvBox.Size = new Size(326, 124);
		pnlSrvBox.BorderColor = Color.FromArgb(40, 60, 95);
		pnlPlay.Controls.Add(pnlSrvBox);

		AddLbl(pnlSrvBox, "🌐  ปลายทางเซิร์ฟเวอร์ & รหัสผ่าน (SERVER ADDRESS)", fSub, 14, 10, colAccent);
		txtIp = AddField(pnlSrvBox, "127.0.0.1", 14, 32, 130);
		txtPort = AddField(pnlSrvBox, "7777", 150, 32, 66);
		txtServerPass = AddField(pnlSrvBox, "รหัส (ถ้ามี)", 222, 32, 90);
		txtServerPass.GotFocus += delegate
		{
			if (txtServerPass.ForeColor == colText && txtServerPass.Text.Length > 0)
			{
				txtServerPass.UseSystemPasswordChar = true;
			}
		};
		txtServerPass.TextChanged += delegate
		{
			if (txtServerPass.ForeColor == colText && txtServerPass.Text.Length > 0)
			{
				txtServerPass.UseSystemPasswordChar = true;
			}
			else
			{
				txtServerPass.UseSystemPasswordChar = false;
			}
		};
		txtServerPass.LostFocus += delegate
		{
			if (txtServerPass.ForeColor == colSub)
			{
				txtServerPass.UseSystemPasswordChar = false;
			}
		};

		txtIp.TextChanged += delegate
		{
			string val = txtIp.Text.Trim();
			if (val.Contains(":"))
			{
				string[] parts = val.Split(':');
				if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
				{
					ForceText(txtIp, parts[0]);
					ForceText(txtPort, parts[1].TrimEnd('/'));
					QueryPlayServer();
				}
			}
		};

		swAuto = new ToggleSwitch();
		swAuto.Location = new Point(16, 82);
		swAuto.CheckedChanged += delegate
		{
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlSrvBox.Controls.Add(swAuto);
		AddLbl(pnlSrvBox, "เชื่อมต่ออัตโนมัติเมื่อเปิด Launcher", fSmall, 68, 85, colSub);

		// 3. Primary Action Button: Full-Width Cyber Yellow (Y=282, H=50, W=668)
		btnConnect = new RoundedBtn("▶   เข้าสู่เกม (LAUNCH GAME)", colYellow, colYellowHi, Color.FromArgb(11, 14, 20), 12, new Font("Segoe UI", 13.5f, FontStyle.Bold));
		btnConnect.BorderColor = Color.FromArgb(255, 245, 120);
		btnConnect.Location = new Point(0, 282);
		btnConnect.Size = new Size(668, 50);
		btnConnect.Click += delegate
		{
			ConnectToServer();
		};
		pnlPlay.Controls.Add(btnConnect);
		base.AcceptButton = btnConnect;

		// 4. Quick Access Server Chips (Y=344, H=42, W=668)
		AddLbl(pnlPlay, "⭐  เซิร์ฟเวอร์โปรด / เข้าเร็ว:", fSub, 0, 344, colSub);
		chipPanel = new TransPanel();
		chipPanel.Location = new Point(0, 368);
		chipPanel.Size = new Size(668, 42);
		pnlPlay.Controls.Add(chipPanel);
	}

	private void ShowNickProfilesMenu(Control anchor)
	{
		ContextMenuStrip cms = new ContextMenuStrip();
		cms.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
		cms.BackColor = colSide;
		cms.ForeColor = colText;

		ToolStripMenuItem header = new ToolStripMenuItem("--- เลือกโปรไฟล์ชื่อผู้เล่น (RP / DM) ---");
		header.Enabled = false;
		cms.Items.Add(header);

		string curNick = FieldVal(txtNick).Trim();
		if (nickProfiles.Count == 0 && curNick.Length > 0)
		{
			nickProfiles.Add(curNick);
		}

		foreach (string prof in nickProfiles)
		{
			string pName = prof;
			ToolStripMenuItem item = new ToolStripMenuItem("👤  " + pName);
			if (string.Equals(pName, curNick, StringComparison.OrdinalIgnoreCase))
			{
				item.Text = "✓  " + pName;
			}
			item.Click += delegate
			{
				ForceText(txtNick, pName);
				SaveConfig();
				ToastOk("เลือกชื่อ: " + pName);
			};
			cms.Items.Add(item);
		}

		cms.Items.Add(new ToolStripSeparator());

		ToolStripMenuItem itemAdd = new ToolStripMenuItem("💾  บันทึกชื่อปัจจุบันลงโปรไฟล์");
		itemAdd.Click += delegate
		{
			string cur = FieldVal(txtNick).Trim();
			if (cur.Length >= 3 && cur.Length <= 20)
			{
				if (!nickProfiles.Contains(cur))
				{
					nickProfiles.Add(cur);
					SaveConfig();
					ToastOk("บันทึกชื่อ " + cur + " ลงโปรไฟล์แล้ว");
				}
				else
				{
					Toast("มีชื่อ " + cur + " ในโปรไฟล์แล้ว");
				}
			}
			else
			{
				ShowErr("กรุณากรอกชื่อผู้เล่นให้ถูกต้อง (3-20 ตัวอักษร)");
			}
		};
		cms.Items.Add(itemAdd);

		if (nickProfiles.Count > 1)
		{
			ToolStripMenuItem itemClear = new ToolStripMenuItem("🗑  ล้างโปรไฟล์อื่นๆ ออก");
			itemClear.Click += delegate
			{
				string cur = FieldVal(txtNick).Trim();
				nickProfiles.Clear();
				if (cur.Length > 0)
				{
					nickProfiles.Add(cur);
				}
				SaveConfig();
				Toast("ล้างโปรไฟล์เรียบร้อย");
			};
			cms.Items.Add(itemClear);
		}

		cms.Show(anchor, new Point(0, anchor.Height));
	}

	private void RebuildChips()
	{
		if (chipPanel == null)
		{
			return;
		}
		chipPanel.Controls.Clear();
		System.Collections.Generic.List<ServerEntry> list = new System.Collections.Generic.List<ServerEntry>();
		list.AddRange(favs);
		list.AddRange(hist);
		int num = 0;
		foreach (ServerEntry item in list)
		{
			if (num < 3)
			{
				ServerEntry ce = item;
				string text = ((ce.Name.Length > 0) ? ce.Name : (ce.Ip + ":" + ce.Port));
				if (text.Length > 24)
				{
					text = text.Substring(0, 23) + "…";
				}
				RoundedBtn roundedBtn = MakeMiniBtn(text, num * 206, 3, 198, 34);
				roundedBtn.Click += delegate
				{
					ForceText(txtIp, ce.Ip);
					ForceText(txtPort, ce.Port);
					ShowPage(0);
					QueryPlayServer();
				};
				chipPanel.Controls.Add(roundedBtn);
				num++;
				continue;
			}
			break;
		}
	}

	private void QueryPlayServer()
	{
		string ip = FieldVal(txtIp);
		string text = FieldVal(txtPort);
		int p;
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(text, out p))
		{
			lblHeroName.Text = "IP / Port ไม่ถูกต้อง";
			lblHeroName.ForeColor = Color.FromArgb(255, 120, 120);
			return;
		}
		string key = ip + ":" + text;
		playQueryKey = key;
		lblHeroName.Text = "กำลังตรวจสอบ " + key + " ...";
		lblHeroName.ForeColor = colSub;
		lblHeroIp.Text = key;
		lblHeroMode.Text = "";
		lblHeroPing.Text = "";
		ring.Ratio = 0f;
		ring.Line1 = "...";
		ring.Line2 = "ตรวจสอบ";
		ring.Invalidate();
		ThreadPool.QueueUserWorkItem(delegate
		{
			QueryResult qr = core.QueryServer(ip, p);
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						if (!(playQueryKey != key))
						{
							ApplyPlayQuery(qr, key);
						}
					});
				}
			}
			catch
			{
			}
		});
	}

	private void ApplyPlayQuery(QueryResult qr, string key)
	{
		if (qr.Online)
		{
			lblHeroName.Text = ((qr.Name.Length > 0) ? qr.Name : key);
			lblHeroName.ForeColor = colOk;
			lblHeroIp.Text = key;
			string text = ((qr.Mode.Length > 0) ? ("โหมด: " + qr.Mode) : "");
			if (qr.IsOpenMp)
			{
				text = text + ((text.Length > 0) ? " • " : "") + "open.mp";
			}
			lblHeroMode.Text = text;
			lblHeroPing.Text = "ปิง " + qr.Ping + " ms • ออนไลน์";
			lblHeroPing.ForeColor = colOk;
			ring.Ratio = ((qr.Max > 0) ? ((float)qr.Players / (float)qr.Max) : 0f);
			ring.Current = qr.Players;
			ring.Max = qr.Max;
			ring.Line1 = qr.Players + "/" + qr.Max;
			ring.Line2 = "ผู้เล่น";
			ring.RingColor = colAccent;
			ring.Invalidate();
			int num = key.LastIndexOf(':');
			if (num <= 0)
			{
				return;
			}
			string ip = key.Substring(0, num);
			string port = key.Substring(num + 1);
			ServerEntry serverEntry = FindEntry(ip, port, false);
			if (serverEntry != null)
			{
				if (qr.IsOpenMp)
				{
					serverEntry.IsOpenMp = true;
				}
				RecordStat(ip, port, qr.Players);
			}
		}
		else
		{
			lblHeroName.Text = key;
			lblHeroName.ForeColor = Color.FromArgb(255, 120, 120);
			lblHeroIp.Text = key;
			lblHeroMode.Text = "";
			lblHeroPing.Text = "ไม่ตอบสนอง (เซิร์ฟเวอร์อาจปิด หรือ IP ผิด)";
			lblHeroPing.ForeColor = colSub;
			ring.Ratio = 0f;
			ring.Line1 = "--";
			ring.Line2 = "ออฟไลน์";
			ring.Invalidate();
		}
	}

	private void ConnectToServer()
	{
		if (connecting)
		{
			return;
		}
		string text = FieldVal(txtNick);
		string text2 = FieldVal(txtIp);
		string text3 = FieldVal(txtPort);
		if (text.Length == 0 || text2.Length == 0 || text3.Length == 0)
		{
			ShowErr("กรุณากรอกชื่อผู้เล่น / IP / Port ให้ครบถ้วน");
			return;
		}
		if (text.Length > 20)
		{
			ShowErr("Nickname ต้องไม่เกิน 20 ตัวอักษร");
			return;
		}
		if (text.Length < 3)
		{
			ShowErr("Nickname ต้องยาวอย่างน้อย 3 ตัวอักษร");
			return;
		}
		foreach (char c in text)
		{
			if (!IsValidNickChar(c))
			{
				ShowErr("Nickname ใช้ตัวอักษรไม่ถูกต้อง\r\n(ใช้ได้เฉพาะ a-z, A-Z, 0-9, _, [], (), $, @, . เท่านั้น)");
				return;
			}
		}
		if (!ValidHost(text2))
		{
			ShowErr("IP หรือ Hostname ไม่ถูกต้อง (เช่น: 1.2.3.4 หรือ th.example.com)");
			return;
		}
		int result;
		if (!int.TryParse(text3, out result) || result < 1 || result > 65535)
		{
			ShowErr("Port ไม่ถูกต้อง (ต้องอยู่ระหว่าง 1-65535)");
			return;
		}
		string text5 = core.ResolveHost(text2);
		if (text5 == null)
		{
			ShowErr("ไม่สามารถค้นหา IP จาก Hostname ได้: " + text2);
			return;
		}
		if (result != 7777 || !text5.StartsWith("127."))
		{
			QueryResult check = core.QueryServer(text5, result);
			if (!check.Online)
			{
				if (MessageBox.Show(this, "เซิร์ฟเวอร์ " + text5 + ":" + result + " ไม่ตอบสนองต่อการตรวจสถานะ (เซิร์ฟเวอร์อาจปิด หรือ Firewall บล็อก Port " + result + " / UDP Query)\r\n\r\nคุณยังต้องการเข้าสู่เกมต่อไปหรือไม่?", "แจ้งเตือนการเชื่อมต่อ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					return;
				}
			}
		}
		string gameDir = GetGameDir();
		string text6 = Path.Combine(gameDir, "gta_sa.exe");
		string text7 = Path.Combine(gameDir, "samp.dll");
		if (!File.Exists(text6))
		{
			ShowErr("ไม่พบ gta_sa.exe ใน:\r\n" + gameDir + "\r\n(ตั้งค่าโฟลเดอร์เกมได้ที่หน้า ⚙)");
			return;
		}
		if (!File.Exists(text7))
		{
			ShowErr("ไม่พบ samp.dll\r\nต้องติดตั้ง SA-MP ไว้ในโฟลเดอร์เกมก่อน");
			return;
		}
		if (Process.GetProcessesByName("gta_sa").Length > 0)
		{
			ShowErr("gta_sa.exe กำลังรันอยู่ ปิดเกมเดิมก่อนเข้าสู่เกมใหม่");
			return;
		}
		try
		{
			SaveIconFromGame(text6);
		}
		catch
		{
		}

		string sPass = (txtServerPass != null) ? FieldVal(txtServerPass) : "";
		ServerEntry matchedEntry = FindEntry(text2, text3, false);
		if (string.IsNullOrEmpty(sPass) && matchedEntry != null && matchedEntry.Locked)
		{
			string prompted = PromptPassword((matchedEntry.Name.Length > 0) ? matchedEntry.Name : (text2 + ":" + text3));
			if (prompted == null)
			{
				return;
			}
			sPass = prompted;
		}

		SaveConfig();
		AddHistory(text2, text3);
		btnConnect.Enabled = false;
		connecting = true;
		SetStatus("กำลังเปิดเกม...");
		Refresh();
		bool flag = false;
		try
		{
			CyberAudio.PlayLaunch();
			StartGameAndInject(text6, text7, gameDir, text, text2, text3, sPass);
			flag = true;
			SetStatus("เกมกำลังทำงาน • เชื่อมต่อ " + text2 + ":" + text3);
		}
		catch (Exception ex)
		{
			ShowErr("เปิดเกมไม่สำเร็จ:\r\n" + ex.Message);
			SetStatus("ล้มเหลว — ลองใหม่อีกครั้ง");
		}
		finally
		{
			connecting = false;
			btnConnect.Enabled = true;
		}
		if (flag)
		{
			StartPlaytime(text2 + "|" + text3);
			string sName = (lblHeroName != null && !string.IsNullOrEmpty(lblHeroName.Text) && !lblHeroName.Text.StartsWith("กรอก") && !lblHeroName.Text.StartsWith("กำลัง")) ? lblHeroName.Text : (text2 + ":" + text3);
			int pCur = (ring != null) ? ring.Current : -1;
			int pMax = (ring != null) ? ring.Max : -1;
			DiscordRpc.UpdateGamePresence(sName, text2, text3, pCur, pMax);
			if (swClose.Checked)
			{
				Close();
			}
			else
			{
				base.WindowState = FormWindowState.Minimized;
			}
		}
	}

	private void StartGameAndInject(string gtaPath, string dllPath, string workDir, string nick, string ip, string port, string pass)
	{
		string sanitized = SanitizeNick(nick);
		if (sanitized.Length < 3 || sanitized.Length > 20)
		{
			ShowErr("Nickname ไม่ถูกต้อง (ต้องยาว 3-20 ตัวอักษร และใช้อักขระที่ SA-MP รองรับ)\r\nชื่อที่กรอก: " + nick);
			return;
		}
		core.LaunchAndInject(gtaPath, dllPath, workDir, sanitized, ip, port, pass);
	}

	private string PromptPassword(string serverName)
	{
		using (Form prompt = new Form())
		{
			prompt.Text = "เซิร์ฟเวอร์มีรหัสผ่าน (Password)";
			prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
			prompt.StartPosition = FormStartPosition.CenterParent;
			prompt.Size = new Size(390, 184);
			prompt.BackColor = Color.FromArgb(17, 17, 24);
			prompt.MaximizeBox = false;
			prompt.MinimizeBox = false;
			prompt.ShowInTaskbar = false;

			Label lbl = new Label
			{
				Text = "🔒 เซิร์ฟเวอร์ " + serverName + " มีรหัสผ่าน:\r\nกรุณากรอกรหัสผ่านเพื่อเข้าสู่เกม:",
				Font = new Font("Segoe UI", 9.5f),
				ForeColor = Color.White,
				Location = new Point(20, 16),
				AutoSize = true
			};
			prompt.Controls.Add(lbl);

			TextBox tb = new TextBox
			{
				Text = "",
				Font = new Font("Segoe UI", 11f),
				BackColor = colField,
				ForeColor = Color.White,
				UseSystemPasswordChar = true,
				BorderStyle = BorderStyle.FixedSingle,
				Location = new Point(22, 58),
				Size = new Size(330, 28)
			};
			prompt.Controls.Add(tb);

			string entered = null;
			RoundedBtn btnOk = new RoundedBtn("เข้าเกม", colAccent, colAccentHi, Color.FromArgb(11, 14, 20), 8, new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold))
			{
				Location = new Point(176, 102),
				Size = new Size(96, 32)
			};
			btnOk.Click += delegate
			{
				entered = tb.Text.Trim();
				prompt.DialogResult = DialogResult.OK;
			};
			prompt.Controls.Add(btnOk);

			RoundedBtn btnCancel = new RoundedBtn("ยกเลิก", Color.FromArgb(36, 36, 52), Color.FromArgb(48, 48, 70), Color.FromArgb(160, 160, 180), 8, new Font("Segoe UI", 9.5f))
			{
				Location = new Point(280, 102),
				Size = new Size(72, 32)
			};
			btnCancel.Click += delegate { prompt.Close(); };
			prompt.Controls.Add(btnCancel);
			prompt.AcceptButton = btnOk;
			prompt.CancelButton = btnCancel;

			if (prompt.ShowDialog(this) == DialogResult.OK)
			{
				return entered ?? "";
			}
			return null;
		}
	}

	private static bool ValidIp(string s)
	{
		string[] array = s.Split('.');
		if (array.Length != 4)
		{
			return false;
		}
		for (int i = 0; i < 4; i++)
		{
			int result;
			if (array[i].Length == 0 || array[i].Length > 3 || !int.TryParse(array[i], out result) || result < 0 || result > 255)
			{
				return false;
			}
		}
		return true;
	}

	private static bool ValidHost(string s)
	{
		if (ValidIp(s))
		{
			return true;
		}
		if (s.Length < 3 || s.Length > 255)
		{
			return false;
		}
		string[] array = s.Split('.');
		if (array.Length < 2)
		{
			return false;
		}
		foreach (string text in array)
		{
			if (text.Length == 0 || text.Length > 63)
			{
				return false;
			}
			foreach (char c in text)
			{
				if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
				{
					return false;
				}
			}
		}
		return true;
	}

	private string RandomNick()
	{
		string[] array = new string[12]
		{
			"Thai", "Pro", "Dark", "Neo", "Max", "Ace", "Xx", "Mr", "Big", "Cyber",
			"Shadow", "Fast"
		};
		string[] array2 = new string[11]
		{
			"Gamer", "Player", "Hunter", "Sniper", "Racer", "King", "Star", "Boy", "XD", "Pro",
			"GG"
		};
		return array[rnd.Next(array.Length)] + array2[rnd.Next(array2.Length)] + rnd.Next(10, 99);
	}

	public static bool IsValidNickChar(char c)
	{
		return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
			c == '_' || c == '[' || c == ']' || c == '(' || c == ')' || c == '$' || c == '@' || c == '.' || c == '=';
	}

	private string SanitizeNick(string raw)
	{
		if (raw == null) return "";
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in raw)
		{
			if (IsValidNickChar(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}
}
