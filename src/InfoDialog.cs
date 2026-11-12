using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

internal class InfoDialog : Form
{
	private ListView lstP;
	private ListView lstR;
	private Label lblLoad;

	public InfoDialog(ServerEntry e)
	{
		Text = "ข้อมูลเซิร์ฟเวอร์";
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.ShowInTaskbar = false;
		base.StartPosition = FormStartPosition.CenterParent;
		base.Size = new Size(500, 560);
		BackColor = Color.FromArgb(17, 17, 24);
		Font = new Font("Segoe UI", 9.5f);

		Label lblTitle = new Label
		{
			Text = "🌐 " + ((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port)),
			Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(51, 204, 255),
			BackColor = Color.Transparent,
			Location = new Point(20, 12),
			AutoSize = true
		};
		base.Controls.Add(lblTitle);

		string subText = e.Ip + ":" + e.Port;
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
			Location = new Point(22, 38),
			AutoSize = true
		};
		base.Controls.Add(lblSub);

		Label lblPlayersHeader = new Label
		{
			Text = "รายชื่อผู้เล่นออนไลน์ (PLAYERS)",
			ForeColor = Color.FromArgb(140, 140, 165),
			BackColor = Color.Transparent,
			Location = new Point(20, 64),
			AutoSize = true
		};
		base.Controls.Add(lblPlayersHeader);

		lstP = new ListView();
		lstP.View = View.Details;
		lstP.BorderStyle = BorderStyle.None;
		lstP.BackColor = Color.FromArgb(26, 26, 38);
		lstP.ForeColor = Color.FromArgb(235, 235, 245);
		lstP.FullRowSelect = true;
		lstP.Location = new Point(20, 88);
		lstP.Size = new Size(444, 200);
		lstP.Columns.Add("ID", 46);
		lstP.Columns.Add("ชื่อผู้เล่น", 204);
		lstP.Columns.Add("คะแนน", 90);
		lstP.Columns.Add("Ping", 86);
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
		lstR.BackColor = Color.FromArgb(26, 26, 38);
		lstR.ForeColor = Color.FromArgb(235, 235, 245);
		lstR.FullRowSelect = true;
		lstR.Location = new Point(20, 324);
		lstR.Size = new Size(444, 148);
		lstR.Columns.Add("คีย์ (Key)", 150);
		lstR.Columns.Add("ค่า (Value)", 290);
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
		if (pl != null)
		{
			if (pl.Count == 0)
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
		}
		lstP.EndUpdate();

		lstR.BeginUpdate();
		lstR.Items.Clear();
		if (rl != null)
		{
			if (rl.Count == 0)
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
		}
		lstR.EndUpdate();
	}
}
