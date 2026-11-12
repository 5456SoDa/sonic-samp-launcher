using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal partial class LauncherForm
{
	private ulong activeScanHandle = 0;
	private System.Windows.Forms.Timer scanPollTimer;
	private bool activeScanIsPublic = false;
	private string pubCachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "public_servers_cache.json");
	private Dictionary<string, ServerEntry> activeScanLookup = new Dictionary<string, ServerEntry>();
	private Dictionary<ServerEntry, ListViewItem> rowByEntry = new Dictionary<ServerEntry, ListViewItem>();

	private void BuildSrvPage()
	{
		// Top Search & Filter Bar (Y=0, H=42, W=668)
		txtSearch = AddField(pnlSrv, "🔍  ค้นหาเซิร์ฟเวอร์ (ชื่อ / IP)...", 0, 0, 316);
		txtSearch.TextChanged += delegate
		{
			filterText = FieldVal(txtSearch).ToLowerInvariant();
			RebuildList();
		};
		txtSearch.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.KeyCode == Keys.Escape)
			{
				ev.SuppressKeyPress = true;
				ForceText(txtSearch, "");
				filterText = "";
				RebuildList();
			}
		};

		btnMine = MakeMiniBtn("★  ของฉัน (0)", 326, 0, 106, 42);
		btnMine.BorderColor = colAccent;
		btnMine.Click += delegate
		{
			ShowView("mine");
		};
		pnlSrv.Controls.Add(btnMine);

		btnPub = MakeMiniBtn("🌐  สาธารณะ", 440, 0, 114, 42);
		btnPub.BorderColor = Color.FromArgb(40, 60, 95);
		btnPub.Click += delegate
		{
			ShowView("public");
		};
		pnlSrv.Controls.Add(btnPub);

		btnRefreshAll = MakeMiniBtn("↻  รีเฟรช", 562, 0, 106, 42);
		btnRefreshAll.BorderColor = colAccent;
		btnRefreshAll.Click += delegate
		{
			RefreshCurrentView();
		};
		pnlSrv.Controls.Add(btnRefreshAll);

		// Filter Chips (shown when public mode is selected)
		btnF1 = MakeMiniBtn("ซ่อนเต็ม", 0, 48, 104, 26);
		btnF2 = MakeMiniBtn("ซ่อนคน 0", 110, 48, 104, 26);
		btnF3 = MakeMiniBtn("ซ่อนมีรหัส", 220, 48, 114, 26);
		btnF1.Click += delegate
		{
			fHideFull = !fHideFull;
			StyleChip(btnF1, fHideFull);
			RebuildList();
		};
		btnF2.Click += delegate
		{
			fHideEmpty = !fHideEmpty;
			StyleChip(btnF2, fHideEmpty);
			RebuildList();
		};
		btnF3.Click += delegate
		{
			fHideLocked = !fHideLocked;
			StyleChip(btnF3, fHideLocked);
			RebuildList();
		};
		StyleChip(btnF1, false);
		StyleChip(btnF2, false);
		StyleChip(btnF3, false);
		btnF1.Visible = false;
		btnF2.Visible = false;
		btnF3.Visible = false;
		pnlSrv.Controls.Add(btnF1);
		pnlSrv.Controls.Add(btnF2);
		pnlSrv.Controls.Add(btnF3);

		// Center Table: ListView (Y=48, H=328, W=668)
		lstSrv = new ListView();
		lstSrv.View = View.Details;
		lstSrv.BorderStyle = BorderStyle.None;
		lstSrv.BackColor = colCard;
		lstSrv.ForeColor = colText;
		lstSrv.Font = fField;
		lstSrv.FullRowSelect = true;
		lstSrv.HideSelection = true;
		lstSrv.ShowItemToolTips = true;
		lstSrv.OwnerDraw = true;
		lstSrv.Location = new Point(0, 48);
		lstSrv.Size = new Size(668, 328);
		lstSrv.SmallImageList = new ImageList();
		lstSrv.SmallImageList.ImageSize = new Size(1, 46);
		lstSrv.Columns.Add("ชื่อเซิร์ฟเวอร์", 354);
		lstSrv.Columns.Add("ผู้เล่น", 96);
		lstSrv.Columns.Add("Ping", 86);
		lstSrv.Columns.Add("ประเภท", 130);
		lstSrv.DoubleClick += delegate
		{
			UseSelectedEntry(true);
		};
		lstSrv.MouseClick += delegate(object s, MouseEventArgs ev)
		{
			if (ev.Button == MouseButtons.Right)
			{
				ListViewItem itemAt = lstSrv.GetItemAt(ev.X, ev.Y);
				if (itemAt != null)
				{
					itemAt.Selected = true;
					itemAt.Focused = true;
				}
			}
		};
		lstSrv.ColumnClick += delegate(object s, ColumnClickEventArgs ev)
		{
			SortByColumn(ev.Column);
		};
		lstSrv.DrawColumnHeader += LstSrv_DrawColumnHeader;
		lstSrv.DrawItem += LstSrv_DrawItem;
		lstSrv.DrawSubItem += LstSrv_DrawSubItem;
		EnableDoubleBuffer(lstSrv);
		pnlSrv.Controls.Add(lstSrv);

		// Empty label
		emptyLbl = new Label();
		emptyLbl.Text = "ยังไม่มีเซิร์ฟเวอร์ในรายการ";
		emptyLbl.Font = fField;
		emptyLbl.ForeColor = colSub;
		emptyLbl.BackColor = colCard;
		emptyLbl.AutoSize = false;
		emptyLbl.Size = new Size(300, 30);
		emptyLbl.TextAlign = ContentAlignment.MiddleCenter;
		emptyLbl.Location = new Point((lstSrv.Width - emptyLbl.Width) / 2, (lstSrv.Height - emptyLbl.Height) / 2);
		emptyLbl.Visible = false;
		lstSrv.Controls.Add(emptyLbl);

		// Bottom Action Bar (Y=382, H=42, W=668)
		RoundedBtn roundedBtn = MakeMiniBtn("+  เพิ่ม", 0, 382, 86, 42);
		roundedBtn.BorderColor = colAccent;
		roundedBtn.Click += delegate
		{
			PromptAddServer();
		};
		pnlSrv.Controls.Add(roundedBtn);

		RoundedBtn roundedBtn2 = MakeMiniBtn("−  ลบ", 92, 382, 76, 42);
		roundedBtn2.BorderColor = Color.FromArgb(40, 60, 95);
		roundedBtn2.Click += delegate
		{
			DeleteEntry();
		};
		pnlSrv.Controls.Add(roundedBtn2);

		RoundedBtn roundedBtn3 = MakeMiniBtn("ℹ  ข้อมูล", 174, 382, 90, 42);
		roundedBtn3.BorderColor = colAccent;
		roundedBtn3.Click += delegate
		{
			ShowServerInfo();
		};
		pnlSrv.Controls.Add(roundedBtn3);

		btnWatch = MakeMiniBtn("🔔  เฝ้าดูที่ว่าง", 270, 382, 126, 42);
		btnWatch.BorderColor = Color.FromArgb(40, 60, 95);
		btnWatch.Click += delegate
		{
			ToggleWatch();
		};
		pnlSrv.Controls.Add(btnWatch);

		RoundedBtn roundedBtn4 = MakeMiniBtn("▷   เล่นเซิร์ฟเวอร์นี้", 404, 382, 264, 42);
		roundedBtn4.SetPalette(colYellow, colYellowHi);
		roundedBtn4.TextColor = Color.FromArgb(11, 14, 20);
		roundedBtn4.BorderColor = Color.FromArgb(255, 245, 120);
		roundedBtn4.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
		roundedBtn4.Click += delegate
		{
			UseSelectedEntry(true);
		};
		pnlSrv.Controls.Add(roundedBtn4);

		// Context Menu
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.BackColor = colCard;
		contextMenuStrip.ForeColor = colText;
		contextMenuStrip.ShowImageMargin = false;
		contextMenuStrip.Font = fSub;
		contextMenuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());

		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("▷   เข้าเกม");
		toolStripMenuItem.Click += delegate
		{
			UseSelectedEntry(true);
		};
		ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("ℹ   ข้อมูลเซิร์ฟเวอร์");
		toolStripMenuItem2.Click += delegate
		{
			ShowServerInfo();
		};
		ToolStripMenuItem toolStripMenuItem3 = new ToolStripMenuItem("★   เพิ่มเป็นรายการโปรด");
		toolStripMenuItem3.Click += delegate
		{
			AddFavorite();
		};
		ToolStripMenuItem toolStripMenuItem4 = new ToolStripMenuItem("⧉   คัดลอก IP");
		toolStripMenuItem4.Click += delegate
		{
			CopySelectedIp();
		};
		miWatch = new ToolStripMenuItem("🔔   เฝ้าดูที่ว่าง");
		miWatch.Click += delegate
		{
			ToggleWatch();
		};
		ToolStripMenuItem toolStripMenuItem5 = new ToolStripMenuItem("📊   สถิติผู้เล่น");
		toolStripMenuItem5.Click += delegate
		{
			ShowStats();
		};
		ToolStripMenuItem toolStripMenuItem6 = new ToolStripMenuItem("🗑   ล้างประวัติทั้งหมด");
		toolStripMenuItem6.Click += delegate
		{
			ClearHistory();
		};
		ToolStripMenuItem toolStripMenuItem7 = new ToolStripMenuItem("−   ลบ");
		toolStripMenuItem7.Click += delegate
		{
			DeleteEntry();
		};

		contextMenuStrip.Items.AddRange(new ToolStripItem[9]
		{
			toolStripMenuItem,
			toolStripMenuItem2,
			toolStripMenuItem3,
			toolStripMenuItem4,
			miWatch,
			toolStripMenuItem5,
			new ToolStripSeparator(),
			toolStripMenuItem7,
			toolStripMenuItem6
		});
		lstSrv.ContextMenuStrip = contextMenuStrip;
	}

	private void CopySelectedIp()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		try
		{
			Clipboard.SetText(serverEntry.Ip + ":" + serverEntry.Port);
			SetStatus("คัดลอก " + serverEntry.Ip + ":" + serverEntry.Port + " แล้ว");
			ToastOk("คัดลอก " + serverEntry.Ip + ":" + serverEntry.Port + " แล้ว");
		}
		catch
		{
		}
	}

	private void LstSrv_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
	{
		e.DrawDefault = false;
		Graphics graphics = e.Graphics;
		graphics.FillRectangle(brHeader, e.Bounds);
		TextRenderer.DrawText(graphics, e.Header.Text, fSub, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height), colSub, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		if (e.ColumnIndex == sortCol)
		{
			TextRenderer.DrawText(graphics, sortDesc ? "▼" : "▲", fSmall, new Point(e.Bounds.Right - 16, e.Bounds.Y + (e.Bounds.Height - 12) / 2), colAccentHi);
		}
		graphics.DrawLine(penHeaderLine, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
	}

	private void LstSrv_DrawItem(object sender, DrawListViewItemEventArgs e)
	{
		e.DrawDefault = false;
		Graphics graphics = e.Graphics;
		graphics.FillRectangle(brCard, e.Bounds);
		if ((e.State & ListViewItemStates.Selected) != 0)
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(e.Bounds.X + 3, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 4);
			using (GraphicsPath path = RoundRect(r, 8))
			{
				graphics.FillPath(brSel, path);
			}
			graphics.FillRectangle(brAccentBar, r.X, r.Y + 6, 3, r.Height - 12);
		}
	}

	private void LstSrv_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
	{
		e.DrawDefault = false;
		ServerEntry serverEntry = e.Item.Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		bool flag = (e.ItemState & ListViewItemStates.Selected) != 0;
		Color foreColor = (flag ? colText : Color.FromArgb(215, 215, 230));
		int num = e.Bounds.Y + e.Bounds.Height / 2;
		if (e.ColumnIndex == 0)
		{
			string text = ((serverEntry.Name.Length > 0) ? serverEntry.Name : (serverEntry.Ip + ":" + serverEntry.Port));
			TextRenderer.DrawText(graphics, text, fField, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 5, e.Bounds.Width - 50, 20), foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			TextRenderer.DrawText(graphics, serverEntry.Ip + ":" + serverEntry.Port, fSmall, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 24, e.Bounds.Width - 18, 18), colSub, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			if (serverEntry.IsOpenMp)
			{
				TextRenderer.DrawText(graphics, "open.mp", fSmall, new Rectangle(e.Bounds.Right - 62, e.Bounds.Y + 4, 52, 20), Color.FromArgb(0, 206, 201), TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
			else if (serverEntry.Locked)
			{
				TextRenderer.DrawText(graphics, "\ud83d\udd12", fSmall, new Rectangle(e.Bounds.Right - 34, e.Bounds.Y + 4, 26, 20), colSub, TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
		}
		else if (e.ColumnIndex == 1)
		{
			string text2 = ((serverEntry.Ping == -2) ? "-" : ((serverEntry.Players < 0) ? "-" : (serverEntry.Players + "/" + serverEntry.Max)));
			Color foreColor2 = ((serverEntry.Players >= 0 && serverEntry.Max > 0 && serverEntry.Players >= serverEntry.Max) ? brPingY.Color : (flag ? colText : Color.FromArgb(200, 200, 215)));
			TextRenderer.DrawText(graphics, text2, fField, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), foreColor2, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
		else if (e.ColumnIndex == 2)
		{
			if (serverEntry.Ping == -2)
			{
				TextRenderer.DrawText(graphics, "...", fField, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), colSub, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
			else if (serverEntry.Ping < 0)
			{
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.FillEllipse(brDotOff, e.Bounds.X + 8, num - 4, 9, 9);
				TextRenderer.DrawText(graphics, "ออฟ", fField, new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height), colSub, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
			else
			{
				SolidBrush solidBrush = ((serverEntry.Ping < 80) ? brPingG : ((serverEntry.Ping < 150) ? brPingY : brPingR));
				Color color = solidBrush.Color;
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.FillEllipse(solidBrush, e.Bounds.X + 8, num - 4, 9, 9);
				TextRenderer.DrawText(graphics, serverEntry.Ping + " ms", fField, new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height), color, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
		}
		else if (e.ColumnIndex == 3)
		{
			string t;
			Color foreColor3;
			SolidBrush brush;
			if (serverEntry.Fav)
			{
				t = "★ โปรด";
				foreColor3 = colAccentHi;
				brush = brPillFav;
			}
			else if (viewMode == "public")
			{
				t = "สาธารณะ";
				foreColor3 = Color.FromArgb(0, 206, 201);
				brush = brPillPub;
			}
			else
			{
				t = "ประวัติ";
				foreColor3 = colSub;
				brush = brPillHist;
			}
			int num2 = PillWidth(t);
			Rectangle rectangle = new Rectangle(e.Bounds.X + 8, num - 11, num2, 22);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.FillPath(brush, RoundRect(rectangle, 11));
			TextRenderer.DrawText(graphics, t, fSub, rectangle, foreColor3, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}

	private bool MatchFilter(ServerEntry e, string f)
	{
		if (f.Length > 0 && !(e.Name + " " + e.Ip + ":" + e.Port + " " + e.Mode).ToLowerInvariant().Contains(f))
		{
			return false;
		}
		if (viewMode == "public")
		{
			if (fHideFull && e.Players >= 0 && e.Max > 0 && e.Players >= e.Max)
			{
				return false;
			}
			if (fHideEmpty && e.Players == 0)
			{
				return false;
			}
			if (fHideLocked && e.Locked)
			{
				return false;
			}
		}
		return true;
	}

	private void SortByColumn(int col)
	{
		if (sortCol == col)
		{
			sortDesc = !sortDesc;
		}
		else
		{
			sortCol = col;
			sortDesc = false;
		}
		Comparison<ServerEntry> comparison = delegate(ServerEntry a, ServerEntry b)
		{
			int num;
			if (col == 0)
			{
				string na = (a.Name.Length > 0) ? a.Name : (a.Ip + ":" + a.Port);
				string nb = (b.Name.Length > 0) ? b.Name : (b.Ip + ":" + b.Port);
				num = string.Compare(na, nb, true);
			}
			else if (col == 1)
			{
				int ap = (a.Players < 0) ? -1 : a.Players;
				int bp = (b.Players < 0) ? -1 : b.Players;
				num = bp.CompareTo(ap);
			}
			else if (col == 2)
			{
				int aping = (a.Ping < 0) ? 999999 : a.Ping;
				int bping = (b.Ping < 0) ? 999999 : b.Ping;
				num = aping.CompareTo(bping);
			}
			else if (col == 3)
			{
				num = b.Fav.CompareTo(a.Fav);
				if (num == 0)
				{
					num = string.Compare(a.Ip + ":" + a.Port, b.Ip + ":" + b.Port, true);
				}
			}
			else
			{
				num = string.Compare(a.Ip + ":" + a.Port, b.Ip + ":" + b.Port, true);
			}
			return (!sortDesc) ? num : (-num);
		};
		if (viewMode == "public")
		{
			pub.Sort(comparison);
		}
		else
		{
			favs.Sort(comparison);
			hist.Sort(comparison);
		}
		RebuildList();
	}

	private void StyleChip(RoundedBtn b, bool on)
	{
		Color color = Anim.Lerp(colField, colAccent, 0.45f);
		b.SetPalette(on ? color : colField, colSel);
		b.TextColor = (on ? colText : colSub);
	}

	private void ShowView(string mode)
	{
		viewMode = mode;
		Color color = Anim.Lerp(colField, colAccent, 0.45f);
		btnMine.SetPalette((mode == "mine") ? color : colField, colField);
		btnMine.TextColor = ((mode == "mine") ? colText : colSub);
		btnPub.SetPalette((mode == "public") ? color : colField, colField);
		btnPub.TextColor = ((mode == "public") ? colText : colSub);
		bool flag = viewMode == "public";
		if (btnF1 != null)
		{
			btnF1.Visible = flag;
			btnF2.Visible = flag;
			btnF3.Visible = flag;
		}
		lstSrv.Location = new Point(0, flag ? 80 : 48);
		lstSrv.Size = new Size(668, flag ? 296 : 328);
		if (emptyLbl != null)
		{
			emptyLbl.Location = lstSrv.Location;
			emptyLbl.Size = lstSrv.Size;
		}
		RebuildList();
		if (flag)
		{
			if (pub.Count == 0)
			{
				FetchPublicServers();
			}
			StartPubAuto();
		}
		else
		{
			StopPubAuto();
		}
	}

	private void StartPubAuto()
	{
		if (pubAuto != null)
		{
			return;
		}
		pubAuto = new System.Windows.Forms.Timer();
		pubAuto.Interval = 60000;
		pubAuto.Tick += delegate
		{
			if (base.IsDisposed)
			{
				StopPubAuto();
			}
			else if (viewMode == "public" && pnlSrv.Visible && !fetchingPub)
			{
				FetchPublicServers();
			}
		};
		pubAuto.Start();
	}

	private void StopPubAuto()
	{
		if (pubAuto != null)
		{
			pubAuto.Stop();
			pubAuto.Dispose();
			pubAuto = null;
		}
	}

	private void RefreshCurrentView()
	{
		if (viewMode == "public")
		{
			FetchPublicServers();
		}
		else
		{
			RefreshAllServers();
		}
	}

	private void FetchPublicServers()
	{
		if (fetchingPub)
		{
			return;
		}
		if (core.SupportsBatchScan)
		{
			CancelActiveScan();
			fetchingPub = true;
			activeScanIsPublic = true;
			activeScanLookup.Clear();
			SetStatus("กำลังดึงรายชื่อเซิร์ฟเวอร์สาธารณะ (Rust Engine)...");
			activeScanHandle = core.StartPublicScan(pubCachePath, 64);
			if (activeScanHandle == 0)
			{
				fetchingPub = false;
				SetStatus("ไม่สามารถเริ่มสแกนเซิร์ฟเวอร์สาธารณะได้");
				return;
			}
			StartScanPollTimer();
			return;
		}
		fetchingPub = true;
		SetStatus("กำลังดึงรายชื่อเซิร์ฟเวอร์สาธารณะ...");
		ThreadPool.QueueUserWorkItem(delegate
		{
			List<ServerEntry> list = new List<ServerEntry>();
			try
			{
				string input;
				using (WebClient webClient = new WebClient())
				{
					webClient.Encoding = Encoding.UTF8;
					input = webClient.DownloadString("https://api.open.mp/servers");
				}
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				object[] array = javaScriptSerializer.Deserialize<object[]>(input);
				foreach (object obj in array)
				{
					Dictionary<string, object> dictionary = obj as Dictionary<string, object>;
					if (dictionary != null)
					{
						object value;
						string text = (dictionary.TryGetValue("ip", out value) ? (value as string) : null);
						if (text != null)
						{
							object value2;
							string text2 = (dictionary.TryGetValue("vn", out value2) ? ((value2 as string) ?? "") : "");
							bool isOpenMp = text2.Length > 0 && !text2.StartsWith("0.3");
							int num = text.LastIndexOf(':');
							if (num > 0)
							{
								ServerEntry serverEntry = new ServerEntry
								{
									Ip = text.Substring(0, num),
									Port = text.Substring(num + 1),
									Fav = false,
									Ping = -2,
									IsOpenMp = isOpenMp
								};
								if (dictionary.TryGetValue("hn", out value))
								{
									serverEntry.Name = (value as string) ?? "";
								}
								if (dictionary.TryGetValue("pc", out value) && value != null)
								{
									serverEntry.Players = Convert.ToInt32(value);
								}
								if (dictionary.TryGetValue("pm", out value) && value != null)
								{
									serverEntry.Max = Convert.ToInt32(value);
								}
								if (dictionary.TryGetValue("gm", out value))
								{
									serverEntry.Mode = (value as string) ?? "";
								}
								if (dictionary.TryGetValue("pa", out value) && value != null)
								{
									try
									{
										serverEntry.Locked = Convert.ToBoolean(value);
									}
									catch
									{
									}
								}
								if (dictionary.TryGetValue("la", out value))
								{
									serverEntry.Language = (value as string) ?? "";
								}
								list.Add(serverEntry);
							}
						}
					}
				}
				list.Sort((ServerEntry a, ServerEntry b) => b.Players.CompareTo(a.Players));
				if (list.Count > 100)
				{
					list.RemoveRange(100, list.Count - 100);
				}
			}
			catch
			{
			}
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						fetchingPub = false;
						if (list.Count > 0)
						{
							pub = list;
						}
						if (viewMode == "public")
						{
							RebuildList();
							if (pub.Count == 0)
							{
								SetStatus("ดึงรายการสาธารณะไม่สำเร็จ — ตรวจอินเทอร์เน็ตแล้วกดรีเฟรชอีกครั้ง");
							}
							else
							{
								SetStatus("เซิร์ฟเวอร์สาธารณะ " + pub.Count + " เซิร์ฟเวอร์ — กำลังตรวจสถานะ...");
							}
						}
						List<ServerEntry> list2 = new List<ServerEntry>(pub);
						foreach (ServerEntry item in list2)
						{
							ServerEntry ee = item;
							int qport;
							if (int.TryParse(ee.Port, out qport))
							{
								ThreadPool.QueueUserWorkItem(delegate
								{
									QueryResult qr = core.QueryServer(ee.Ip, qport);
									try
									{
										if (!base.IsDisposed)
										{
											BeginInvoke((MethodInvoker)delegate
											{
												ApplyEntryQuery(ee, qr);
											});
										}
									}
									catch
									{
									}
								});
							}
						}
					});
				}
			}
			catch
			{
				fetchingPub = false;
			}
		});
	}

	private void RefreshAllServers()
	{
		if (querying || !base.IsHandleCreated)
		{
			return;
		}
		querying = true;
		btnRefreshAll.Enabled = false;
		btnRefreshAll.Text = "กำลังตรวจ...";
		List<ServerEntry> list = new List<ServerEntry>();
		list.AddRange(favs);
		list.AddRange(hist);
		if (list.Count == 0)
		{
			FinishRefresh();
			return;
		}
		if (core.SupportsBatchScan)
		{
			CancelActiveScan();
			activeScanIsPublic = false;
			activeScanLookup.Clear();
			foreach (ServerEntry item in list)
			{
				activeScanLookup[item.Ip + ":" + item.Port] = item;
			}
			activeScanHandle = core.StartScan(list, 64);
			if (activeScanHandle == 0)
			{
				FinishRefresh();
				return;
			}
			StartScanPollTimer();
			return;
		}
		int remaining = list.Count;
		foreach (ServerEntry item in list)
		{
			ServerEntry ee = item;
			int port;
			if (!int.TryParse(ee.Port, out port))
			{
				if (Interlocked.Decrement(ref remaining) == 0)
				{
					FinishRefresh();
				}
				continue;
			}
			ThreadPool.QueueUserWorkItem(delegate
			{
				QueryResult qr = core.QueryServer(ee.Ip, port);
				try
				{
					if (!base.IsDisposed)
					{
						BeginInvoke((MethodInvoker)delegate
						{
							ApplyEntryQuery(ee, qr);
						});
					}
				}
				catch
				{
				}
				if (Interlocked.Decrement(ref remaining) == 0)
				{
					FinishRefresh();
				}
			});
		}
	}

	private void FinishRefresh()
	{
		try
		{
			if (!base.IsDisposed)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					querying = false;
					btnRefreshAll.Enabled = true;
					btnRefreshAll.Text = "↻   รีเฟรช";
				});
			}
		}
		catch
		{
		}
	}

	private void ShowServerInfo()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			SetStatus("เลือกเซิร์ฟเวอร์ก่อน");
			return;
		}
		ServerEntry e = lstSrv.SelectedItems[0].Tag as ServerEntry;
		int port;
		if (e == null || !int.TryParse(e.Port, out port))
		{
			return;
		}
		InfoDialog dlg = new InfoDialog(e);
		dlg.Show(this);
		ThreadPool.QueueUserWorkItem(delegate
		{
			List<string[]> pl = core.QueryPlayers(e.Ip, port);
			List<string[]> rl = core.QueryRules(e.Ip, port);
			try
			{
				if (!base.IsDisposed && !dlg.IsDisposed)
				{
					dlg.BeginInvoke((MethodInvoker)delegate
					{
						dlg.SetData(pl, rl);
					});
				}
			}
			catch
			{
			}
		});
	}

	private void StartScanPollTimer()
	{
		if (scanPollTimer == null)
		{
			scanPollTimer = new System.Windows.Forms.Timer();
			scanPollTimer.Interval = 50;
			scanPollTimer.Tick += delegate
			{
				PollActiveScan();
			};
		}
		scanPollTimer.Start();
	}

	private void CancelActiveScan()
	{
		if (scanPollTimer != null)
		{
			scanPollTimer.Stop();
		}
		if (activeScanHandle != 0)
		{
			core.CancelScan(activeScanHandle);
			activeScanHandle = 0;
		}
		if (activeScanIsPublic)
		{
			fetchingPub = false;
		}
	}

	private void PollActiveScan()
	{
		if (activeScanHandle == 0 || base.IsDisposed)
		{
			if (scanPollTimer != null) scanPollTimer.Stop();
			return;
		}
		ScanPollResult batch = core.PollScan(activeScanHandle, 64);
		if (batch.List != null)
		{
			pub = batch.List;
			activeScanLookup.Clear();
			foreach (ServerEntry se in pub)
			{
				activeScanLookup[se.Ip + ":" + se.Port] = se;
			}
			if (viewMode == "public")
			{
				RebuildList();
				if (batch.FromCache)
				{
					SetStatus("โหมดออฟไลน์ — แสดงรายชื่อจากแคช " + pub.Count + " เซิร์ฟเวอร์");
				}
				else
				{
					SetStatus("เซิร์ฟเวอร์สาธารณะ " + pub.Count + " เซิร์ฟเวอร์ — กำลังตรวจ Ping...");
				}
			}
		}
		if (batch.Updates != null && batch.Updates.Count > 0)
		{
			lstSrv.BeginUpdate();
			try
			{
				foreach (ScanUpdateItem u in batch.Updates)
				{
					string key = u.Ip + ":" + u.Port;
					ServerEntry entry;
					if (!activeScanLookup.TryGetValue(key, out entry))
					{
						continue;
					}
					if (u.Online)
					{
						entry.Name = u.Name;
						entry.Players = u.Players;
						entry.Max = u.Max;
						entry.Ping = u.Ping;
						if (u.IsOpenMp)
						{
							entry.IsOpenMp = true;
						}
						if (entry.Fav || hist.Contains(entry))
						{
							RecordStat(entry.Ip, entry.Port, u.Players);
						}
					}
					else
					{
						if (!activeScanIsPublic || entry.Players < 0)
						{
							entry.Players = -1;
						}
						entry.Ping = -1;
					}
					ListViewItem lvi;
					if (rowByEntry.TryGetValue(entry, out lvi))
					{
						PaintEntry(lvi, entry);
					}
				}
			}
			finally
			{
				lstSrv.EndUpdate();
			}
		}
		if (batch.Error != null && batch.Error.Length > 0 && activeScanIsPublic && pub.Count == 0)
		{
			if (viewMode == "public")
			{
				SetStatus("ดึงรายการสาธารณะไม่สำเร็จ — ตรวจอินเทอร์เน็ตแล้วกดรีเฟรชอีกครั้ง");
			}
		}
		if (batch.Done)
		{
			if (scanPollTimer != null) scanPollTimer.Stop();
			activeScanHandle = 0;
			if (activeScanIsPublic)
			{
				fetchingPub = false;
				if (viewMode == "public" && pub.Count > 0 && !batch.FromCache)
				{
					SetStatus("เซิร์ฟเวอร์สาธารณะ " + pub.Count + " เซิร์ฟเวอร์ • ตรวจสถานะครบแล้ว");
				}
			}
			else
			{
				FinishRefresh();
			}
		}
	}

	private void ApplyEntryQuery(ServerEntry e, QueryResult qr)
	{
		if (qr.Online)
		{
			e.Name = qr.Name;
			e.Players = qr.Players;
			e.Max = qr.Max;
			e.Ping = qr.Ping;
			if (qr.IsOpenMp)
			{
				e.IsOpenMp = true;
			}
			if (e.Fav || hist.Contains(e))
			{
				RecordStat(e.Ip, e.Port, qr.Players);
			}
		}
		else
		{
			e.Players = -1;
			e.Ping = -1;
		}
		ListViewItem lvi;
		if (rowByEntry.TryGetValue(e, out lvi))
		{
			PaintEntry(lvi, e);
		}
	}

	private void PaintEntry(ListViewItem it, ServerEntry e)
	{
		it.SubItems[0].Text = ((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port));
		it.SubItems[1].Text = ((e.Ping == -2) ? "-" : ((e.Players < 0) ? "-" : (e.Players + "/" + e.Max)));
		it.SubItems[2].Text = ((e.Ping == -2) ? "..." : ((e.Ping < 0) ? "ออฟ" : (e.Ping + " ms")));
		it.SubItems[2].ForeColor = ((e.Ping <= 0) ? colSub : ((e.Ping < 120) ? colOk : Color.FromArgb(243, 156, 18)));
		it.SubItems[3].Text = e.Ip + ":" + e.Port;
		it.SubItems[4].Text = (e.Fav ? "★ โปรด" : ((viewMode == "public") ? "สาธารณะ" : "ประวัติ"));
		it.SubItems[4].ForeColor = (e.Fav ? colAccentHi : colSub);
		string text = PlaytimeText(e.Ip, e.Port);
		string text2 = e.Mode;
		if (e.IsOpenMp)
		{
			text2 = text2 + ((text2.Length > 0) ? " • " : "") + "open.mp";
		}
		if (text2.Length == 0)
		{
			text2 = e.Ip + ":" + e.Port;
		}
		it.ToolTipText = text2 + ((text.Length > 0) ? ("\n" + text) : "");
	}

	private void RebuildList()
	{
		ServerEntry serverEntry = ((lstSrv.SelectedItems.Count > 0) ? (lstSrv.SelectedItems[0].Tag as ServerEntry) : null);
		lstSrv.BeginUpdate();
		lstSrv.Items.Clear();
		rowByEntry.Clear();
		if (viewMode == "public")
		{
			foreach (ServerEntry item in pub)
			{
				if (MatchFilter(item, filterText))
				{
					AddEntryRow(item);
				}
			}
		}
		else
		{
			foreach (ServerEntry fav in favs)
			{
				if (MatchFilter(fav, filterText))
				{
					AddEntryRow(fav);
				}
			}
			foreach (ServerEntry item2 in hist)
			{
				if (MatchFilter(item2, filterText))
				{
					AddEntryRow(item2);
				}
			}
		}
		lstSrv.EndUpdate();
		if (btnMine != null)
		{
			btnMine.Text = "★  ของฉัน (" + (favs.Count + hist.Count) + ")";
		}
		if (serverEntry != null)
		{
			foreach (ListViewItem item3 in lstSrv.Items)
			{
				if (object.ReferenceEquals(item3.Tag, serverEntry))
				{
					item3.Selected = true;
					item3.Focused = true;
					break;
				}
			}
		}
		if (emptyLbl != null)
		{
			emptyLbl.Visible = lstSrv.Items.Count == 0;
			if (viewMode == "public")
			{
				emptyLbl.Text = "กำลังโหลดรายการ...\r\nถ้าไม่ขึ้น ลองกด ↻ รีเฟรชอีกครั้ง";
			}
			else
			{
				emptyLbl.Text = "ยังไม่มีรายการ\r\nกด \ud83c\udf10 สาธารณะ เพื่อเลือกเซิร์ฟเวอร์ หรือกรอก IP ในหน้าเล่นเกมแล้วกด + เพิ่มโปรด";
			}
		}
	}

	private void AddEntryRow(ServerEntry e)
	{
		ListViewItem listViewItem = new ListViewItem((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port));
		listViewItem.SubItems.Add((e.Players < 0) ? "-" : (e.Players + "/" + e.Max));
		listViewItem.SubItems.Add((e.Ping < 0) ? "ออฟ" : (e.Ping + " ms"));
		listViewItem.SubItems.Add(e.Ip + ":" + e.Port);
		listViewItem.SubItems.Add(e.Fav ? "★ โปรด" : "ประวัติ");
		listViewItem.Tag = e;
		lstSrv.Items.Add(listViewItem);
		rowByEntry[e] = listViewItem;
		PaintEntry(listViewItem, e);
	}

	private ServerEntry FindEntry(string ip, string port, bool favOnly)
	{
		foreach (ServerEntry fav in favs)
		{
			if (fav.Ip == ip && fav.Port == port)
			{
				return fav;
			}
		}
		if (!favOnly)
		{
			foreach (ServerEntry item in hist)
			{
				if (item.Ip == ip && item.Port == port)
				{
					return item;
				}
			}
		}
		return null;
	}

	private void AddFavorite()
	{
		string ip;
		string text;
		if (lstSrv.SelectedItems.Count > 0)
		{
			ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
			if (serverEntry == null)
			{
				return;
			}
			ip = serverEntry.Ip;
			text = serverEntry.Port;
		}
		else
		{
			ip = FieldVal(txtIp);
			text = FieldVal(txtPort);
		}
		int p;
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(text, out p) || p < 1 || p > 65535)
		{
			ShowErr("กรอก IP และ Port ในหน้าเล่นเกมก่อน");
			return;
		}
		if (FindEntry(ip, text, false) != null)
		{
			SetStatus("เซิร์ฟเวอร์นี้มีอยู่ในรายการแล้ว");
			return;
		}
		ServerEntry e = new ServerEntry();
		e.Ip = ip;
		e.Port = text;
		e.Fav = true;
		favs.Add(e);
		RemoveFromHist(ip, text);
		SaveConfig();
		if (viewMode == "public")
		{
			ShowView("mine");
		}
		else
		{
			RebuildList();
		}
		ToastOk("เพิ่ม " + ip + ":" + text + " เป็นรายการโปรดแล้ว");
		ThreadPool.QueueUserWorkItem(delegate
		{
			QueryResult qr = core.QueryServer(ip, p);
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						ApplyEntryQuery(e, qr);
					});
				}
			}
			catch
			{
			}
		});
	}

	private void PromptAddServer()
	{
		if (lstSrv.SelectedItems.Count > 0)
		{
			AddFavorite();
			return;
		}
		using (Form prompt = new Form())
		{
			prompt.Text = "เพิ่มเซิร์ฟเวอร์โปรด (Add Favorite Server)";
			prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
			prompt.StartPosition = FormStartPosition.CenterParent;
			prompt.Size = new Size(420, 192);
			prompt.BackColor = Color.FromArgb(17, 17, 24);
			prompt.MaximizeBox = false;
			prompt.MinimizeBox = false;
			prompt.ShowInTaskbar = false;

			Label lbl = new Label
			{
				Text = "กรอก IP:Port หรือ Hostname ของเซิร์ฟเวอร์:",
				Font = new Font("Segoe UI", 9.5f),
				ForeColor = Color.White,
				Location = new Point(20, 18),
				AutoSize = true
			};
			prompt.Controls.Add(lbl);

			TextBox tb = new TextBox
			{
				Text = "",
				Font = new Font("Segoe UI", 10.5f),
				BackColor = colField,
				ForeColor = Color.White,
				BorderStyle = BorderStyle.FixedSingle,
				Location = new Point(22, 48),
				Size = new Size(360, 28)
			};
			prompt.Controls.Add(tb);

			RoundedBtn btnOk = new RoundedBtn("เพิ่มโปรด", colAccent, colAccentHi, Color.FromArgb(11, 14, 20), 8, new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold))
			{
				Location = new Point(190, 96),
				Size = new Size(100, 34)
			};
			btnOk.Click += delegate
			{
				string val = tb.Text.Trim();
				if (val.Length == 0) return;
				string addIp = val;
				string addPort = "7777";
				if (val.Contains(":"))
				{
					string[] parts = val.Split(':');
					addIp = parts[0];
					addPort = parts[1];
				}
				int p;
				if (!ValidHost(addIp) || !int.TryParse(addPort, out p) || p < 1 || p > 65535)
				{
					MessageBox.Show(prompt, "รูปแบบ IP หรือ Port ไม่ถูกต้อง (เช่น: 127.0.0.1:7777)", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return;
				}
				if (FindEntry(addIp, addPort, false) != null)
				{
					MessageBox.Show(prompt, "เซิร์ฟเวอร์นี้มีอยู่ในรายการแล้ว", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				ServerEntry e = new ServerEntry
				{
					Ip = addIp,
					Port = addPort,
					Fav = true
				};
				favs.Add(e);
				RemoveFromHist(addIp, addPort);
				SaveConfig();
				if (viewMode == "public")
				{
					ShowView("mine");
				}
				else
				{
					RebuildList();
				}
				ToastOk("เพิ่ม " + addIp + ":" + addPort + " เป็นรายการโปรดแล้ว");
				prompt.DialogResult = DialogResult.OK;
				ThreadPool.QueueUserWorkItem(delegate
				{
					QueryResult qr = core.QueryServer(addIp, p);
					try
					{
						if (!base.IsDisposed)
						{
							BeginInvoke((MethodInvoker)delegate { ApplyEntryQuery(e, qr); });
						}
					}
					catch { }
				});
			};
			prompt.Controls.Add(btnOk);

			RoundedBtn btnCancel = new RoundedBtn("ยกเลิก", Color.FromArgb(36, 36, 52), Color.FromArgb(48, 48, 70), Color.FromArgb(160, 160, 180), 8, new Font("Segoe UI", 9.5f))
			{
				Location = new Point(300, 96),
				Size = new Size(82, 34)
			};
			btnCancel.Click += delegate { prompt.Close(); };
			prompt.Controls.Add(btnCancel);
			prompt.AcceptButton = btnOk;
			prompt.CancelButton = btnCancel;

			prompt.ShowDialog(this);
		}
	}

	private void RemoveFromHist(string ip, string port)
	{
		for (int num = hist.Count - 1; num >= 0; num--)
		{
			if (hist[num].Ip == ip && hist[num].Port == port)
			{
				hist.RemoveAt(num);
			}
		}
	}

	private void DeleteEntry()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		if (viewMode == "public")
		{
			SetStatus("รายการสาธารณะลบไม่ได้ • ใช้ปุ่ม + เพิ่มโปรดแทน");
			return;
		}
		if (serverEntry.Fav)
		{
			favs.Remove(serverEntry);
		}
		else
		{
			hist.Remove(serverEntry);
		}
		SaveConfig();
		RebuildList();
		RebuildChips();
		Toast("ลบ " + serverEntry.Ip + ":" + serverEntry.Port + " แล้ว");
	}

	private void UseSelectedEntry(bool andConnect)
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry != null)
		{
			ForceText(txtIp, serverEntry.Ip);
			ForceText(txtPort, serverEntry.Port);
			ShowPage(0);
			QueryPlayServer();
			if (andConnect)
			{
				ConnectToServer();
			}
		}
	}

	private void AddHistory(string ip, string port)
	{
		ServerEntry serverEntry = FindEntry(ip, port, false);
		if (serverEntry == null || !serverEntry.Fav)
		{
			if (serverEntry != null)
			{
				hist.Remove(serverEntry);
			}
			ServerEntry serverEntry2 = new ServerEntry();
			serverEntry2.Ip = ip;
			serverEntry2.Port = port;
			serverEntry2.Fav = false;
			hist.Insert(0, serverEntry2);
			while (hist.Count > 10)
			{
				hist.RemoveAt(hist.Count - 1);
			}
			SaveConfig();
			RebuildList();
			RebuildChips();
		}
	}

	private void RecordStat(string ip, string port, int players)
	{
		try
		{
			string newStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
			string newEntry = ip + "|" + port + "|" + newStamp + "|" + players;
			if (lastRecordedStat == newEntry)
			{
				return;
			}
			lastRecordedStat = newEntry;
			core.RecordStat(ip, port, newStamp, players);
		}
		catch
		{
		}
	}

	private void ClearHistory()
	{
		if (hist.Count == 0)
		{
			SetStatus("ไม่มีประวัติให้ล้าง");
		}
		else if (MessageBox.Show(this, "ลบประวัติเซิร์ฟเวอร์ทั้งหมด? (" + hist.Count + " รายการ)\r\nรายการโปรดไม่ถูกลบ", "ยืนยัน", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
		{
			hist.Clear();
			SaveConfig();
			RebuildList();
			RebuildChips();
			Toast("ล้างประวัติแล้ว");
		}
	}

	private void ShowStats()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			SetStatus("เลือกเซิร์ฟเวอร์ก่อน");
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		List<int> list = new List<int>();
		try
		{
			list = core.GetStats(serverEntry.Ip, serverEntry.Port, 500);
		}
		catch
		{
		}
		if (list.Count < 2)
		{
			SetStatus("ข้อมูลสถิติยังไม่พอ — ใช้งานไปสักพักแล้วกดดูใหม่");
		}
		else
		{
			new StatsDialog((serverEntry.Name.Length > 0) ? serverEntry.Name : (serverEntry.Ip + ":" + serverEntry.Port), list, PlaytimeText(serverEntry.Ip, serverEntry.Port)).Show(this);
		}
	}

	private void StartPlaytime(string key)
	{
		StopPlaytime();
		lock (ptLock)
		{
			ptServer = key;
			if (!playtime.ContainsKey(key))
			{
				playtime[key] = 0L;
			}
			ptTimer = new System.Threading.Timer(PlaytimeTick, null, 30000, 30000);
		}
	}

	private void StopPlaytime()
	{
		System.Threading.Timer timer;
		bool hadServer;
		lock (ptLock)
		{
			timer = ptTimer;
			ptTimer = null;
			hadServer = (ptServer != null);
			ptServer = null;
		}
		if (timer != null)
		{
			timer.Dispose();
		}
		if (hadServer)
		{
			DiscordRpc.UpdateLauncherPresence();
		}
	}

	private void PlaytimeTick(object s)
	{
		try
		{
			if (base.IsDisposed)
			{
				StopPlaytime();
				return;
			}
			string text;
			lock (ptLock)
			{
				text = ptServer;
			}
			if (text == null)
			{
				return;
			}
			if (Process.GetProcessesByName("gta_sa").Length == 0)
			{
				StopPlaytime();
				return;
			}
			long total = core.AddPlaytime(text, 30L);
			lock (ptLock)
			{
				if (ptServer == text)
				{
					if (total > 0)
					{
						playtime[text] = total;
					}
					else
					{
						playtime[text] = (playtime.ContainsKey(text) ? (playtime[text] + 30) : 30);
					}
				}
			}
		}
		catch
		{
		}
	}

	private void LoadPlaytime()
	{
		try
		{
			core.InitStorage(dbPath, playtimeFile, statsPath);
			Dictionary<string, long> loaded = core.LoadAllPlaytime();
			lock (ptLock)
			{
				playtime.Clear();
				foreach (KeyValuePair<string, long> kv in loaded)
				{
					if (kv.Value > 0)
					{
						playtime[kv.Key] = kv.Value;
					}
				}
			}
		}
		catch
		{
		}
	}

	private string PlaytimeText(string ip, string port)
	{
		long value;
		lock (ptLock)
		{
			if (!playtime.TryGetValue(ip + "|" + port, out value) || value < 60)
			{
				return "";
			}
		}
		int num = (int)(value / 3600);
		int num2 = (int)(value % 3600 / 60);
		return "เล่นสะสม " + ((num > 0) ? (num + " ชม. ") : "") + num2 + " นาที";
	}

	private void ToggleWatch()
	{
		if (watchTimer != null)
		{
			StopWatch("หยุดเฝ้าดูที่ว่างแล้ว");
			return;
		}
		if (lstSrv.SelectedItems.Count == 0)
		{
			SetStatus("เลือกเซิร์ฟเวอร์ก่อน");
			return;
		}
		ServerEntry e = lstSrv.SelectedItems[0].Tag as ServerEntry;
		int port;
		if (e == null || !int.TryParse(e.Port, out port))
		{
			return;
		}
		watchEntry = e;
		SetStatus("กำลังตรวจ " + e.Ip + ":" + e.Port + "...");
		ThreadPool.QueueUserWorkItem(delegate
		{
			QueryResult qr = core.QueryServer(e.Ip, port);
			if (!base.IsDisposed)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					if (watchEntry == e)
					{
						if (qr.Online && qr.Players < qr.Max)
						{
							watchEntry = null;
							ShowSlotToast(e, qr.Players, qr.Max);
						}
						else
						{
							watchTimer = new System.Threading.Timer(WatchTick, e, 8000, 25000);
							if (miWatch != null)
							{
								miWatch.Text = "■   หยุดเฝ้าดู (" + e.Ip + ":" + e.Port + ")";
							}
							if (btnWatch != null)
							{
								btnWatch.Text = "■  หยุดเฝ้าดู";
								btnWatch.SetPalette(Color.FromArgb(180, 40, 50), Color.FromArgb(220, 50, 60));
								btnWatch.BorderColor = Color.FromArgb(255, 99, 99);
							}
							string text = (qr.Online ? (qr.Players + "/" + qr.Max) : "ออฟไลน์");
							SetStatus("🔔 กำลังเฝ้าดู " + e.Ip + ":" + e.Port + " (" + text + ") — เช็กทุก 25 วินาที มีที่ว่างจะแจ้งทันที");
						}
					}
				});
			}
		});
	}

	private void WatchTick(object state)
	{
		ServerEntry e = state as ServerEntry;
		int result;
		if (e == null || watchTimer == null || !int.TryParse(e.Port, out result))
		{
			return;
		}
		QueryResult qr = core.QueryServer(e.Ip, result);
		if (!qr.Online || qr.Players >= qr.Max)
		{
			return;
		}
		System.Threading.Timer timer = watchTimer;
		watchTimer = null;
		if (timer != null)
		{
			timer.Dispose();
		}
		if (base.IsDisposed)
		{
			return;
		}
		BeginInvoke((MethodInvoker)delegate
		{
			if (miWatch != null)
			{
				miWatch.Text = "🔔   เฝ้าดูที่ว่าง";
			}
			if (btnWatch != null)
			{
				btnWatch.Text = "🔔  เฝ้าดูที่ว่าง";
				btnWatch.SetPalette(colField, colFieldHover);
				btnWatch.BorderColor = Color.FromArgb(40, 60, 95);
			}
			ShowSlotToast(e, qr.Players, qr.Max);
			CyberAudio.PlayAlert();
			if (autoJoinSlot)
			{
				ForceText(txtIp, e.Ip);
				ForceText(txtPort, e.Port);
				ShowPage(0);
				ConnectToServer();
			}
		});
	}

	private void StopWatch(string msg)
	{
		System.Threading.Timer timer = watchTimer;
		watchTimer = null;
		watchEntry = null;
		if (timer != null)
		{
			timer.Dispose();
		}
		if (miWatch != null)
		{
			miWatch.Text = "🔔   เฝ้าดูที่ว่าง";
		}
		if (btnWatch != null)
		{
			btnWatch.Text = "🔔  เฝ้าดูที่ว่าง";
			btnWatch.SetPalette(colField, colFieldHover);
			btnWatch.BorderColor = Color.FromArgb(40, 60, 95);
		}
		SetStatus(msg);
	}

	private void ShowSlotToast(ServerEntry e, int players, int max)
	{
		base.WindowState = FormWindowState.Normal;
		Activate();
		SetStatus("✅ มีที่ว่างแล้ว! " + e.Ip + ":" + e.Port + " (" + players + "/" + max + ")");
		ToastForm toastForm = new ToastForm("มีที่ว่างแล้ว!", ((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port)) + "\n" + e.Ip + ":" + e.Port + "  •  " + players + "/" + max + " คน");
		toastForm.OnJoin += delegate
		{
			ForceText(txtIp, e.Ip);
			ForceText(txtPort, e.Port);
			ShowPage(0);
			ConnectToServer();
		};
		toastForm.Show(this);
	}
}
