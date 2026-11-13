using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

internal class InfoDialog : Form
{
	private ListView lstP;
	private ListView lstR;
	private Label lblLoad;
	private Font fHeader;

	public InfoDialog(ServerEntry e)
	{
		Text = "ข้อมูลเซิร์ฟเวอร์";
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.CenterParent;
		base.ClientSize = DpiHelper.Sz(484, 526);
		BackColor = Color.FromArgb(17, 17, 24);
		Font = new Font("Segoe UI", 9.5f);
		fHeader = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold);

		Label lblTitle = new Label
		{
			Text = "🌐 " + ((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port)),
			Font = new Font("Segoe UI Semibold", 12.0f, FontStyle.Bold),
			ForeColor = Color.FromArgb(51, 204, 255),
			BackColor = Color.Transparent,
			Location = new Point(20, 12),
			Size = new Size(444, 26),
			AutoEllipsis = true
		};
		base.Controls.Add(lblTitle);

		string subText = e.Ip + ":" + e.Port;
		if (!string.IsNullOrEmpty(e.Mode))
		{
			subText += "  •  " + e.Mode;
		}
		if (!string.IsNullOrEmpty(e.Language))
		{
			subText += "  •  " + e.Language;
		}
		if (e.IsOpenMp)
		{
			subText += "  •  open.mp";
		}
		if (e.Locked)
		{
			subText += "  •  🔒 มีรหัสผ่าน";
		}

		Label lblSub = new Label
		{
			Text = subText,
			Font = new Font("Segoe UI", 9f),
			ForeColor = Color.FromArgb(140, 165, 195),
			BackColor = Color.Transparent,
			Location = new Point(22, 40),
			Size = new Size(442, 20),
			AutoEllipsis = true
		};
		base.Controls.Add(lblSub);

		Label lblPlayersHeader = new Label
		{
			Text = "รายชื่อผู้เล่นออนไลน์ (PLAYERS)",
			ForeColor = Color.FromArgb(140, 140, 165),
			BackColor = Color.Transparent,
			Location = new Point(20, 66),
			AutoSize = true
		};
		base.Controls.Add(lblPlayersHeader);

		lstP = new ListView();
		lstP.View = View.Details;
		lstP.BorderStyle = BorderStyle.None;
		lstP.BackColor = Color.FromArgb(24, 26, 38);
		lstP.ForeColor = Color.FromArgb(235, 235, 245);
		lstP.FullRowSelect = true;
		lstP.Location = new Point(20, 88);
		lstP.Size = new Size(444, 200);
		lstP.Columns.Add("ID", DpiHelper.S(46));
		lstP.Columns.Add("ชื่อผู้เล่น", DpiHelper.S(212));
		lstP.Columns.Add("Score", DpiHelper.S(86));
		lstP.Columns.Add("Ping", DpiHelper.S(80));
		ApplyDarkOwnerDraw(lstP);
		base.Controls.Add(lstP);

		Label lblRulesHeader = new Label
		{
			Text = "กฎและข้อมูลระบบ (SERVER RULES)",
			ForeColor = Color.FromArgb(140, 140, 165),
			BackColor = Color.Transparent,
			Location = new Point(20, 300),
			AutoSize = true
		};
		base.Controls.Add(lblRulesHeader);

		lstR = new ListView();
		lstR.View = View.Details;
		lstR.BorderStyle = BorderStyle.None;
		lstR.BackColor = Color.FromArgb(24, 26, 38);
		lstR.ForeColor = Color.FromArgb(235, 235, 245);
		lstR.FullRowSelect = true;
		lstR.Location = new Point(20, 322);
		lstR.Size = new Size(444, 148);
		lstR.Columns.Add("คีย์ (Key)", DpiHelper.S(150));
		lstR.Columns.Add("ค่า (Value)", DpiHelper.S(274));
		ApplyDarkOwnerDraw(lstR);
		base.Controls.Add(lstR);

		lblLoad = new Label();
		lblLoad.Text = "กำลังโหลดข้อมูลจากเซิร์ฟเวอร์...";
		lblLoad.ForeColor = Color.FromArgb(140, 140, 165);
		lblLoad.BackColor = Color.Transparent;
		lblLoad.Location = new Point(20, 486);
		lblLoad.AutoSize = true;
		base.Controls.Add(lblLoad);

		RoundedBtn roundedBtn = new RoundedBtn("ปิด (Esc)", Color.FromArgb(51, 204, 255), Color.FromArgb(102, 224, 255), Color.FromArgb(11, 14, 20), 8, new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold));
		roundedBtn.Location = new Point(364, 478);
		roundedBtn.Size = new Size(100, 34);
		roundedBtn.Click += delegate
		{
			Close();
		};
		base.Controls.Add(roundedBtn);
		base.AcceptButton = roundedBtn;
		base.CancelButton = roundedBtn;
		DpiHelper.ScaleControlTree(this);
	}

	private void ApplyDarkOwnerDraw(ListView lv)
	{
		lv.OwnerDraw = true;
		lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
		lv.ColumnWidthChanging += delegate(object sender, ColumnWidthChangingEventArgs ev)
		{
			int minW = DpiHelper.S(36);
			if (ev.NewWidth < minW)
			{
				ev.NewWidth = minW;
				ev.Cancel = true;
			}
		};
		lv.DrawColumnHeader += delegate(object sender, DrawListViewColumnHeaderEventArgs ev)
		{
			ev.DrawDefault = false;
			using (SolidBrush brHdr = new SolidBrush(Color.FromArgb(20, 25, 38)))
			{
				ev.Graphics.FillRectangle(brHdr, ev.Bounds);
			}
			using (Pen penLine = new Pen(Color.FromArgb(44, 56, 82), 1f))
			{
				ev.Graphics.DrawLine(penLine, ev.Bounds.Left, ev.Bounds.Bottom - 1, ev.Bounds.Right, ev.Bounds.Bottom - 1);
			}
			Rectangle textBounds = new Rectangle(ev.Bounds.X + DpiHelper.S(6), ev.Bounds.Y, Math.Max(1, ev.Bounds.Width - DpiHelper.S(10)), ev.Bounds.Height);
			TextRenderer.DrawText(ev.Graphics, ev.Header.Text, fHeader, textBounds, Color.FromArgb(145, 180, 215), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
		};
		lv.DrawItem += delegate(object sender, DrawListViewItemEventArgs ev)
		{
			ev.DrawDefault = false;
		};
		lv.DrawSubItem += delegate(object sender, DrawListViewSubItemEventArgs ev)
		{
			ev.DrawDefault = false;
			Rectangle cellBounds = (ev.ColumnIndex == 0 && lv.Columns.Count > 0)
				? new Rectangle(ev.Bounds.X, ev.Bounds.Y, Math.Min(ev.Bounds.Width, lv.Columns[0].Width), ev.Bounds.Height)
				: ev.Bounds;
			Color bgCol = ev.Item.Selected ? Color.FromArgb(34, 62, 92) : ((ev.ItemIndex % 2 == 0) ? Color.FromArgb(24, 26, 38) : Color.FromArgb(28, 31, 45));
			using (SolidBrush brRow = new SolidBrush(bgCol))
			{
				ev.Graphics.FillRectangle(brRow, cellBounds);
			}
			Color fgCol = ev.Item.Selected ? Color.White : Color.FromArgb(228, 234, 245);
			Rectangle textBounds = new Rectangle(cellBounds.X + DpiHelper.S(6), cellBounds.Y, Math.Max(1, cellBounds.Width - DpiHelper.S(10)), cellBounds.Height);
			TextRenderer.DrawText(ev.Graphics, ev.SubItem.Text, lv.Font, textBounds, fgCol, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
		};
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && fHeader != null)
		{
			fHeader.Dispose();
			fHeader = null;
		}
		base.Dispose(disposing);
	}

	public void SetData(List<string[]> pl, List<string[]> rl)
	{
		Color foreColor = Color.FromArgb(90, 210, 130);
		Color foreColor2 = Color.FromArgb(255, 99, 99);
		if (pl == null && rl == null)
		{
			lblLoad.Text = "เซิร์ฟเวอร์ไม่ตอบสนองต่อ Query";
			lblLoad.ForeColor = foreColor2;
			return;
		}
		lblLoad.Text = "โหลดข้อมูลแล้ว ✓";
		lblLoad.ForeColor = foreColor;

		lstP.BeginUpdate();
		lstP.Items.Clear();
		if (pl == null)
		{
			ListViewItem offlineItem = new ListViewItem("-");
			offlineItem.SubItems.Add("ไม่สามารถโหลดรายชื่อผู้เล่น (Timeout/ปิด Query)");
			offlineItem.SubItems.Add("-");
			offlineItem.SubItems.Add("-");
			lstP.Items.Add(offlineItem);
		}
		else if (pl.Count == 0)
		{
			ListViewItem emptyItem = new ListViewItem("-");
			emptyItem.SubItems.Add("ไม่มีผู้เล่นออนไลน์ หรือเซิร์ฟเวอร์ซ่อนรายชื่อ");
			emptyItem.SubItems.Add("-");
			emptyItem.SubItems.Add("-");
			lstP.Items.Add(emptyItem);
		}
		else
		{
			foreach (string[] item in pl)
			{
				if (item.Length >= 4)
				{
					// [ID, Name, Score, Ping]
					ListViewItem lvi = new ListViewItem(item[0]);
					lvi.SubItems.Add(item[1]);
					lvi.SubItems.Add(item[2]);
					lvi.SubItems.Add((item[3] != "?" && !item[3].EndsWith("ms")) ? (item[3] + " ms") : item[3]);
					lstP.Items.Add(lvi);
				}
				else if (item.Length == 3)
				{
					// [Name, Score, Ping]
					ListViewItem lvi = new ListViewItem("-");
					lvi.SubItems.Add(item[0]);
					lvi.SubItems.Add(item[1]);
					lvi.SubItems.Add(item[2]);
					lstP.Items.Add(lvi);
				}
			}
		}
		lstP.EndUpdate();

		lstR.BeginUpdate();
		lstR.Items.Clear();
		if (rl == null)
		{
			ListViewItem offlineRule = new ListViewItem("-");
			offlineRule.SubItems.Add("ไม่สามารถโหลดกฎเซิร์ฟเวอร์ได้");
			lstR.Items.Add(offlineRule);
		}
		else if (rl.Count == 0)
		{
			ListViewItem emptyRule = new ListViewItem("-");
			emptyRule.SubItems.Add("ไม่มีข้อมูลกฎ");
			lstR.Items.Add(emptyRule);
		}
		else
		{
			foreach (string[] item2 in rl)
			{
				lstR.Items.Add(new ListViewItem(item2));
			}
		}
		lstR.EndUpdate();
	}
}
