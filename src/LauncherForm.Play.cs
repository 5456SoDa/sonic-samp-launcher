using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal partial class LauncherForm
{
	private SmoothPanel pnlHeroCard;
	private Label lblHeroTip;
	private RoundedBtn btnHeroRefresh;
	private SmoothPanel pnlNickBox;
	private SmoothPanel pnlSrvBox;
	private RoundedBtn btnNickProfiles;
	private RoundedBtn btnNickRand;
	private Label lblAutoConnect;
	private Label lblQuickTitle;

	private void BuildPlayPage()
	{
		// 1. Hero Server Card (Y=0, H=148, W=756 base)
		pnlHeroCard = new SmoothPanel(colCard, 12);
		pnlHeroCard.BorderColor = Color.FromArgb(45, 68, 105);
		pnlHeroCard.Location = new Point(0, 0);
		pnlHeroCard.Size = new Size(756, 148);
		pnlPlay.Controls.Add(pnlHeroCard);

		lblHeroName = new Label();
		lblHeroName.Text = "กรอก IP เพื่อตรวจสถานะเซิร์ฟเวอร์";
		lblHeroName.Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold);
		lblHeroName.ForeColor = Color.White;
		lblHeroName.BackColor = Color.Transparent;
		lblHeroName.Location = new Point(20, 14);
		lblHeroName.Size = new Size(375, 28);
		lblHeroName.AutoEllipsis = true;
		pnlHeroCard.Controls.Add(lblHeroName);

		lblHeroIp = new Label();
		lblHeroIp.Text = "—";
		lblHeroIp.Font = new Font("Segoe UI Semibold", 9.5f);
		lblHeroIp.ForeColor = colAccent;
		lblHeroIp.BackColor = Color.Transparent;
		lblHeroIp.Location = new Point(20, 44);
		lblHeroIp.AutoSize = true;
		pnlHeroCard.Controls.Add(lblHeroIp);

		lblHeroMode = new Label();
		lblHeroMode.Text = "";
		lblHeroMode.Font = new Font("Segoe UI Semibold", 9f);
		lblHeroMode.ForeColor = colSub;
		lblHeroMode.BackColor = Color.Transparent;
		lblHeroMode.Location = new Point(20, 68);
		lblHeroMode.Size = new Size(375, 20);
		lblHeroMode.AutoEllipsis = true;
		pnlHeroCard.Controls.Add(lblHeroMode);

		lblHeroPing = new Label();
		lblHeroPing.Text = "";
		lblHeroPing.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		lblHeroPing.ForeColor = colOk;
		lblHeroPing.BackColor = Color.Transparent;
		lblHeroPing.Location = new Point(20, 92);
		lblHeroPing.AutoSize = true;
		pnlHeroCard.Controls.Add(lblHeroPing);

		lblHeroTip = new Label();
		lblHeroTip.Text = "* ปิด Frame Limiter ในเกม เพื่อเล่นได้ 144 FPS อย่างลื่นไหล";
		lblHeroTip.Font = fSmall;
		lblHeroTip.ForeColor = colSub;
		lblHeroTip.BackColor = Color.Transparent;
		lblHeroTip.Location = new Point(20, 118);
		lblHeroTip.Size = new Size(375, 18);
		lblHeroTip.AutoEllipsis = true;
		pnlHeroCard.Controls.Add(lblHeroTip);

		heroSpark = new SparklineGraph();
		heroSpark.CompactMode = true;
		heroSpark.AccentColor = colAccent;
		heroSpark.Location = new Point(406, 24);
		heroSpark.Size = new Size(188, 100);
		pnlHeroCard.Controls.Add(heroSpark);

		btnHeroRefresh = MakeMiniBtn("↻", 602, 56, 36, 36);
		btnHeroRefresh.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
		btnHeroRefresh.BorderColor = colAccent;
		btnHeroRefresh.Click += delegate
		{
			QueryPlayServer();
		};
		pnlHeroCard.Controls.Add(btnHeroRefresh);

		ring = new RingGauge();
		ring.Location = new Point(646, 24);
		ring.Size = new Size(100, 100);
		ring.RingColor = colAccent;
		pnlHeroCard.Controls.Add(ring);

		// 2. Middle: Symmetrical 2-Column Boxes (Y=160, H=128)
		pnlNickBox = new SmoothPanel(colCard, 10);
		pnlNickBox.Location = new Point(0, 160);
		pnlNickBox.Size = new Size(370, 128);
		pnlNickBox.BorderColor = Color.FromArgb(40, 60, 95);
		pnlPlay.Controls.Add(pnlNickBox);

		AddLbl(pnlNickBox, "👤  ข้อมูลตัวละคร (PLAYER IDENTITY)", fSub, 14, 10, colAccent);
		txtNick = AddField(pnlNickBox, "ชื่อเล่น: MyNick_TH", 14, 34, 342);

		btnNickProfiles = MakeMiniBtn("👤  โปรไฟล์ชื่อ", 14, 84, 166, 34);
		btnNickProfiles.BorderColor = colAccent;
		btnNickProfiles.Click += delegate
		{
			ShowNickProfilesMenu(btnNickProfiles);
		};
		pnlNickBox.Controls.Add(btnNickProfiles);

		btnNickRand = MakeMiniBtn("🎲  สุ่มชื่อ", 190, 84, 166, 34);
		btnNickRand.BorderColor = Color.FromArgb(40, 60, 95);
		btnNickRand.Click += delegate
		{
			ForceText(txtNick, RandomNick());
		};
		pnlNickBox.Controls.Add(btnNickRand);

		// Right Box: Target Server
		pnlSrvBox = new SmoothPanel(colCard, 10);
		pnlSrvBox.Location = new Point(386, 160);
		pnlSrvBox.Size = new Size(370, 128);
		pnlSrvBox.BorderColor = Color.FromArgb(40, 60, 95);
		pnlPlay.Controls.Add(pnlSrvBox);

		AddLbl(pnlSrvBox, "🌐  ปลายทางเซิร์ฟเวอร์ & รหัสผ่าน (SERVER ADDRESS)", fSub, 14, 10, colAccent);
		txtIp = AddField(pnlSrvBox, "127.0.0.1", 14, 34, 150);
		txtPort = AddField(pnlSrvBox, "7777", 170, 34, 68);
		txtServerPass = AddField(pnlSrvBox, "รหัส (ถ้ามี)", 244, 34, 112);
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
			string normalized = val;
			if (normalized.StartsWith("samp://", StringComparison.OrdinalIgnoreCase))
			{
				normalized = normalized.Substring(7);
			}
			int slash = normalized.IndexOf('/');
			if (slash >= 0)
			{
				normalized = normalized.Substring(0, slash);
			}
			int colon = normalized.LastIndexOf(':');
			if (colon > 0)
			{
				string hostPart = normalized.Substring(0, colon).Trim();
				string portPart = normalized.Substring(colon + 1).Trim();
				int parsedPort;
				if (hostPart.Length > 0 && int.TryParse(portPart, out parsedPort) && parsedPort >= 1 && parsedPort <= 65535)
				{
					ForceText(txtIp, hostPart);
					ForceText(txtPort, parsedPort.ToString());
					QueryPlayServer();
					return;
				}
			}
			if (normalized != val && normalized.Length > 0)
			{
				ForceText(txtIp, normalized);
				QueryPlayServer();
			}
		};

		swAuto = new ToggleSwitch();
		swAuto.Location = new Point(16, 86);
		swAuto.CheckedChanged += delegate
		{
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlSrvBox.Controls.Add(swAuto);
		lblAutoConnect = AddLbl(pnlSrvBox, "เชื่อมต่ออัตโนมัติเมื่อเปิด Launcher", fSmall, 68, 90, colSub);

		// 3. Primary Action Button: Full-Width Cyber Yellow (Y=302, H=54, W=756)
		btnConnect = new RoundedBtn("▶   เข้าสู่เกม (LAUNCH GAME)", colYellow, colYellowHi, Color.FromArgb(11, 14, 20), 12, new Font("Segoe UI", 13.5f, FontStyle.Bold));
		btnConnect.BorderColor = Color.FromArgb(255, 245, 120);
		btnConnect.Location = new Point(0, 302);
		btnConnect.Size = new Size(756, 54);
		btnConnect.Click += delegate
		{
			ConnectToServer();
		};
		pnlPlay.Controls.Add(btnConnect);
		base.AcceptButton = btnConnect;

		// 4. Quick Access Server Chips (Y=370..436, W=756)
		lblQuickTitle = AddLbl(pnlPlay, "⭐  เซิร์ฟเวอร์โปรด / เข้าเร็ว:", fSub, 0, 370, colSub);
		chipPanel = new TransPanel();
		chipPanel.Location = new Point(0, 392);
		chipPanel.Size = new Size(756, 44);
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

	private void LayoutPlayPage(int pageW, int pageH)
	{
		if (pnlHeroCard == null) return;

		int extraH = Math.Max(0, pageH - DpiHelper.S(460));
		int heroH = DpiHelper.S(148) + Math.Min(DpiHelper.S(40), extraH / 3);
		int gapY = DpiHelper.S(12) + Math.Min(DpiHelper.S(8), extraH / 6);
		int gapX = DpiHelper.S(16);

		// 1. Hero Card
		pnlHeroCard.Location = new Point(0, 0);
		pnlHeroCard.Size = new Size(pageW, heroH);

		int pad = DpiHelper.S(16);
		int ringSz = Math.Min(DpiHelper.S(108), heroH - DpiHelper.S(36));
		ring.Size = new Size(ringSz, ringSz);
		ring.Location = new Point(pageW - pad - ringSz, (heroH - ringSz) / 2);

		int refBtnSz = DpiHelper.S(36);
		btnHeroRefresh.Size = new Size(refBtnSz, refBtnSz);
		btnHeroRefresh.Location = new Point(ring.Left - DpiHelper.S(12) - refBtnSz, (heroH - refBtnSz) / 2);

		int infoW = Math.Max(DpiHelper.S(250), Math.Min(DpiHelper.S(420), (int)(pageW * 0.48)));
		int labelX = DpiHelper.S(20);
		int textMaxW = infoW - labelX - DpiHelper.S(8);
		lblHeroName.Location = new Point(labelX, DpiHelper.S(14));
		lblHeroName.Size = new Size(textMaxW, DpiHelper.S(28));
		lblHeroIp.Location = new Point(labelX, DpiHelper.S(44));
		lblHeroMode.Location = new Point(labelX, DpiHelper.S(68));
		lblHeroMode.Size = new Size(textMaxW, DpiHelper.S(20));
		lblHeroPing.Location = new Point(labelX, DpiHelper.S(92));
		if (lblHeroTip != null)
		{
			lblHeroTip.Location = new Point(labelX, heroH - DpiHelper.S(26));
			lblHeroTip.Size = new Size(textMaxW, DpiHelper.S(18));
		}

		int sparkX = infoW + DpiHelper.S(6);
		int sparkW = Math.Max(DpiHelper.S(120), btnHeroRefresh.Left - DpiHelper.S(12) - sparkX);
		heroSpark.Location = new Point(sparkX, DpiHelper.S(18));
		heroSpark.Size = new Size(sparkW, heroH - DpiHelper.S(36));

		// 2. Symmetrical 2-Column Middle Cards
		int midY = heroH + gapY;
		int midH = DpiHelper.S(128);
		int leftW = (pageW - gapX) / 2;
		int rightW = pageW - leftW - gapX;

		pnlNickBox.Location = new Point(0, midY);
		pnlNickBox.Size = new Size(leftW, midH);
		int inPad = DpiHelper.S(14);
		int boxH = DpiHelper.S(42);
		int btnH = DpiHelper.S(34);
		if (txtNick.Parent != null)
		{
			txtNick.Parent.Location = new Point(inPad, DpiHelper.S(34));
			txtNick.Parent.Size = new Size(leftW - inPad * 2, boxH);
		}
		int halfBtnW = (leftW - inPad * 2 - DpiHelper.S(10)) / 2;
		btnNickProfiles.Location = new Point(inPad, DpiHelper.S(84));
		btnNickProfiles.Size = new Size(halfBtnW, btnH);
		btnNickRand.Location = new Point(inPad + halfBtnW + DpiHelper.S(10), DpiHelper.S(84));
		btnNickRand.Size = new Size(leftW - inPad - btnNickRand.Left, btnH);

		pnlSrvBox.Location = new Point(leftW + gapX, midY);
		pnlSrvBox.Size = new Size(rightW, midH);
		int srvInnerW = rightW - inPad * 2;
		int fieldGap = DpiHelper.S(6);
		int portW = DpiHelper.S(68);
		int ipW = Math.Max(DpiHelper.S(105), (int)((srvInnerW - portW - fieldGap * 2) * 0.57));
		int passW = Math.Max(DpiHelper.S(70), srvInnerW - ipW - portW - fieldGap * 2);
		if (txtIp.Parent != null)
		{
			txtIp.Parent.Location = new Point(inPad, DpiHelper.S(34));
			txtIp.Parent.Size = new Size(ipW, boxH);
		}
		if (txtPort.Parent != null)
		{
			txtPort.Parent.Location = new Point(inPad + ipW + fieldGap, DpiHelper.S(34));
			txtPort.Parent.Size = new Size(portW, boxH);
		}
		if (txtServerPass.Parent != null)
		{
			txtServerPass.Parent.Location = new Point(inPad + ipW + portW + fieldGap * 2, DpiHelper.S(34));
			txtServerPass.Parent.Size = new Size(passW, boxH);
		}
		swAuto.Location = new Point(DpiHelper.S(16), DpiHelper.S(86));
		swAuto.Size = new Size(DpiHelper.S(44), DpiHelper.S(24));
		if (lblAutoConnect != null)
		{
			lblAutoConnect.Location = new Point(DpiHelper.S(68), DpiHelper.S(90));
		}

		// 3. Launch Button
		int connY = midY + midH + gapY;
		int connH = DpiHelper.S(54) + Math.Min(DpiHelper.S(8), extraH / 8);
		btnConnect.Location = new Point(0, connY);
		btnConnect.Size = new Size(pageW, connH);

		// 4. Quick Access Server Chips
		int quickY = connY + connH + gapY;
		if (lblQuickTitle != null)
		{
			lblQuickTitle.Location = new Point(0, quickY);
		}
		chipPanel.Location = new Point(0, quickY + DpiHelper.S(22));
		chipPanel.Size = new Size(pageW, DpiHelper.S(44));
		LayoutQuickChips();
	}

	private void LayoutQuickChips()
	{
		if (chipPanel == null || chipPanel.Controls.Count == 0) return;
		int count = chipPanel.Controls.Count;
		int gap = DpiHelper.S(10);
		int chipW = Math.Max(DpiHelper.S(140), (chipPanel.Width - gap * (count - 1)) / Math.Max(3, count));
		int chipH = DpiHelper.S(36);
		for (int i = 0; i < count; i++)
		{
			Control c = chipPanel.Controls[i];
			c.Location = new Point(i * (chipW + gap), DpiHelper.S(3));
			c.Size = new Size(chipW, chipH);
		}
	}

	private void ResumePlaySpark()
	{
		if (txtIp == null || txtPort == null) return;
		string ip = FieldVal(txtIp);
		string portStr = FieldVal(txtPort);
		int p;
		if (ip.Length > 0 && ValidHost(ip) && int.TryParse(portStr, out p) && p > 0 && p <= 65535)
		{
			StartActiveSpark(ip, p);
		}
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
			if (num < 4)
			{
				ServerEntry ce = item;
				string text = ((ce.Name.Length > 0) ? ce.Name : (ce.Ip + ":" + ce.Port));
				if (text.Length > 24)
				{
					text = text.Substring(0, 23) + "…";
				}
				RoundedBtn roundedBtn = MakeMiniBtn(text, num * DpiHelper.S(184), DpiHelper.S(3), DpiHelper.S(176), DpiHelper.S(36));
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
		LayoutQuickChips();
	}

	private void QueryPlayServer()
	{
		string ip = FieldVal(txtIp);
		string text = FieldVal(txtPort);
		int p;
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(text, out p) || p <= 0 || p > 65535)
		{
			lblHeroName.Text = "IP / Port ไม่ถูกต้อง";
			lblHeroName.ForeColor = Color.FromArgb(255, 120, 120);
			if (btnConnect != null) btnConnect.PulseGlow = false;
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
		if (btnConnect != null) btnConnect.PulseGlow = false;
		StartActiveSpark(ip, p);
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
			if (btnConnect != null) btnConnect.PulseGlow = true;
			int num = key.LastIndexOf(':');
			if (num <= 0)
			{
				return;
			}
			string ip = key.Substring(0, num);
			string port = key.Substring(num + 1);
			int pNum;
			if (int.TryParse(port, out pNum) && pNum > 0 && pNum <= 65535)
			{
				StartActiveSpark(ip, pNum);
			}
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
			if (btnConnect != null) btnConnect.PulseGlow = false;
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

		connecting = true;
		btnConnect.Enabled = false;
		SetStatus("กำลังตรวจสอบสถานะเซิร์ฟเวอร์ " + text2 + ":" + result + "...");
		ThreadPool.QueueUserWorkItem(delegate
		{
			string resolvedIp = core.ResolveHost(text2);
			QueryResult check = null;
			if (resolvedIp != null && (result != 7777 || !resolvedIp.StartsWith("127.")))
			{
				check = core.QueryServer(resolvedIp, result);
			}
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						FinishConnectAfterCheck(text, text2, text3, result, resolvedIp, check, gameDir, text6, text7);
					});
				}
			}
			catch
			{
			}
		});
	}

	private void FinishConnectAfterCheck(string nick, string host, string portStr, int port, string resolvedIp, QueryResult check, string gameDir, string gtaExe, string sampDll)
	{
		if (resolvedIp == null)
		{
			connecting = false;
			btnConnect.Enabled = true;
			SetStatus("ล้มเหลว — ไม่พบ IP ของ " + host);
			ShowErr("ไม่สามารถค้นหา IP จาก Hostname ได้: " + host);
			return;
		}
		if (check != null && !check.Online)
		{
			if (MessageBox.Show(this, "เซิร์ฟเวอร์ " + resolvedIp + ":" + port + " ไม่ตอบสนองต่อการตรวจสถานะ (เซิร์ฟเวอร์อาจปิด หรือ Firewall บล็อก Port " + port + " / UDP Query)\r\n\r\nคุณยังต้องการเข้าสู่เกมต่อไปหรือไม่?", "แจ้งเตือนการเชื่อมต่อ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
			{
				connecting = false;
				btnConnect.Enabled = true;
				SetStatus("ยกเลิกการเชื่อมต่อ");
				return;
			}
		}
		try
		{
			SaveIconFromGame(gtaExe);
		}
		catch
		{
		}

		string sPass = (txtServerPass != null) ? FieldVal(txtServerPass) : "";
		ServerEntry matchedEntry = FindEntry(host, portStr, false);
		if (string.IsNullOrEmpty(sPass) && matchedEntry != null && matchedEntry.Locked)
		{
			string dName = (check != null && !string.IsNullOrEmpty(check.Name)) ? check.Name : ((matchedEntry.Name.Length > 0) ? matchedEntry.Name : (host + ":" + portStr));
			string prompted = PromptPassword(dName);
			if (prompted == null)
			{
				connecting = false;
				btnConnect.Enabled = true;
				SetStatus("ยกเลิกการเชื่อมต่อ");
				return;
			}
			sPass = prompted;
		}

		SaveConfig();
		AddHistory(host, portStr);
		SetStatus("กำลังเปิดเกม...");
		Refresh();
		bool flag = false;
		try
		{
			CyberAudio.PlayLaunch();
			StartGameAndInject(gtaExe, sampDll, gameDir, nick, host, portStr, sPass);
			flag = true;
			SetStatus("เกมกำลังทำงาน • เชื่อมต่อ " + host + ":" + portStr);
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
			bool closeAfter = swClose.Checked;
			StartPlaytime(host + "|" + portStr, closeAfter);
			string sName = (check != null && check.Online && check.Name.Length > 0) ? check.Name : ((lblHeroName != null && !string.IsNullOrEmpty(lblHeroName.Text) && !lblHeroName.Text.StartsWith("กรอก") && !lblHeroName.Text.StartsWith("กำลัง")) ? lblHeroName.Text : (host + ":" + portStr));
			int pCur = (check != null && check.Online) ? check.Players : ((ring != null) ? ring.Current : -1);
			int pMax = (check != null && check.Online) ? check.Max : ((ring != null) ? ring.Max : -1);
			DiscordRpc.UpdateGamePresence(sName, host, portStr, pCur, pMax);
			if (closeAfter)
			{
				core.StopPingSpark();
				if (sparkPollTimer != null) sparkPollTimer.Stop();
				StopPubAuto();
				base.ShowInTaskbar = false;
				Hide();
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
