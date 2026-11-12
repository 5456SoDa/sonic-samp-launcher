using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal class ChatlogDialog : Form
{
	private RichTextBox txtLog;
	private TextBox txtSearch;
	private Label lblInfo;
	private List<string> allLines = new List<string>();
	private string chatlogPath;

	public ChatlogDialog()
	{
		Text = "ประวัติแชท SA-MP (Chatlog Viewer)";
		BackColor = Color.FromArgb(11, 14, 20);
		base.StartPosition = FormStartPosition.CenterParent;
		base.Size = new Size(720, 520);
		base.ShowInTaskbar = false;
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;

		chatlogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP", "chatlog.txt");

		Font fTitle = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold);
		Font fSub = new Font("Segoe UI", 9f);
		Font fField = new Font("Segoe UI", 10f);
		Font fBtn = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);

		Color colSonic = Color.FromArgb(51, 204, 255);
		Color colYellow = Color.FromArgb(255, 235, 0);

		Label lblTitle = new Label
		{
			Text = "📜 ประวัติแชท SA-MP (Cyber Chatlog Viewer)",
			Font = fTitle,
			ForeColor = colSonic,
			BackColor = Color.Transparent,
			Location = new Point(20, 14),
			AutoSize = true
		};
		base.Controls.Add(lblTitle);

		lblInfo = new Label
		{
			Text = "กำลังโหลดข้อมูล...",
			Font = fSub,
			ForeColor = Color.FromArgb(140, 165, 195),
			BackColor = Color.Transparent,
			Location = new Point(22, 42),
			AutoSize = true
		};
		base.Controls.Add(lblInfo);

		// Search Field
		txtSearch = new TextBox
		{
			Location = new Point(20, 70),
			Size = new Size(460, 28),
			BackColor = Color.FromArgb(16, 22, 34),
			ForeColor = Color.White,
			BorderStyle = BorderStyle.FixedSingle,
			Font = fField
		};
		txtSearch.TextChanged += delegate { FilterLines(); };
		base.Controls.Add(txtSearch);

		RoundedBtn btnReload = new RoundedBtn("↻ รีเฟรช", Color.FromArgb(19, 27, 43), Color.FromArgb(30, 48, 75), Color.White, 8, fBtn);
		btnReload.BorderColor = colSonic;
		btnReload.Location = new Point(490, 68);
		btnReload.Size = new Size(95, 30);
		btnReload.Click += delegate { LoadLines(); };
		base.Controls.Add(btnReload);

		RoundedBtn btnClear = new RoundedBtn("✕ ล้างค้นหา", Color.FromArgb(19, 27, 43), Color.FromArgb(30, 48, 75), Color.White, 8, fBtn);
		btnClear.Location = new Point(595, 68);
		btnClear.Size = new Size(100, 30);
		btnClear.Click += delegate { txtSearch.Text = ""; };
		base.Controls.Add(btnClear);

		// RichTextBox Log viewer
		txtLog = new RichTextBox
		{
			Location = new Point(20, 110),
			Size = new Size(675, 315),
			BackColor = Color.FromArgb(16, 22, 34),
			ForeColor = Color.FromArgb(235, 245, 255),
			BorderStyle = BorderStyle.None,
			Font = new Font("Consolas", 9.5f),
			ReadOnly = true,
			WordWrap = false,
			ScrollBars = RichTextBoxScrollBars.Both
		};
		base.Controls.Add(txtLog);

		// Bottom actions
		RoundedBtn btnCopy = new RoundedBtn("📋 คัดลอกทั้งหมด", colYellow, Color.FromArgb(255, 250, 80), Color.FromArgb(11, 14, 20), 8, fBtn);
		btnCopy.Location = new Point(20, 436);
		btnCopy.Size = new Size(130, 34);
		btnCopy.Click += delegate
		{
			if (!string.IsNullOrEmpty(txtLog.Text))
			{
				try
				{
					Clipboard.SetText(txtLog.Text);
					MessageBox.Show(this, "คัดลอกข้อความในบันทึกแชทไปยังคลิปบอร์ดแล้ว", "คัดลอกสำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
				}
				catch { }
			}
		};
		base.Controls.Add(btnCopy);

		RoundedBtn btnOpenDir = new RoundedBtn("📂 เปิดโฟลเดอร์แชท", Color.FromArgb(19, 27, 43), Color.FromArgb(30, 48, 75), Color.White, 8, fBtn);
		btnOpenDir.BorderColor = colSonic;
		btnOpenDir.Location = new Point(160, 436);
		btnOpenDir.Size = new Size(140, 34);
		btnOpenDir.Click += delegate
		{
			try
			{
				string folder = Path.GetDirectoryName(chatlogPath);
				if (Directory.Exists(folder))
				{
					Process.Start("explorer.exe", folder);
				}
				else
				{
					MessageBox.Show(this, "ยังไม่พบโฟลเดอร์ SAMP ใน MyDocuments\nเข้าเล่นเกมก่อนเพื่อให้ตัวเกมสร้างโฟลเดอร์", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
			catch { }
		};
		base.Controls.Add(btnOpenDir);

		RoundedBtn btnClose = new RoundedBtn("ปิด", colSonic, Color.FromArgb(102, 224, 255), Color.FromArgb(11, 14, 20), 8, fBtn);
		btnClose.Location = new Point(595, 436);
		btnClose.Size = new Size(100, 34);
		btnClose.Click += delegate { Close(); };
		base.Controls.Add(btnClose);

		LoadLines();
	}

	private void LoadLines()
	{
		allLines.Clear();
		if (!File.Exists(chatlogPath))
		{
			lblInfo.Text = "ยังไม่พบไฟล์ chatlog.txt (ไฟล์จะถูกสร้างอัตโนมัติเมื่อเข้าเล่นในเซิร์ฟเวอร์)";
			txtLog.Text = "--- ยังไม่มีบันทึกแชท ---\nเข้าเล่นในเซิร์ฟเวอร์ SA-MP เพื่อเริ่มบันทึกบทสนทนา";
			return;
		}

		try
		{
			ChatTailResult res = ChatLogReader.ReadTail(chatlogPath, 2 * 1024 * 1024, 5000);
			allLines = res.Lines;
			lblInfo.Text = string.Format("พบไฟล์ chatlog.txt (แสดง {0} บรรทัดล่าสุด)", allLines.Count);
			FilterLines();
		}
		catch (Exception ex)
		{
			lblInfo.Text = "เกิดข้อผิดพลาดในการอ่านไฟล์: " + ex.Message;
		}
	}

	private void FilterLines()
	{
		string query = txtSearch.Text.Trim();
		StringBuilder sb = new StringBuilder();
		int matchCount = 0;

		for (int i = 0; i < allLines.Count; i++)
		{
			string line = allLines[i];
			if (string.IsNullOrEmpty(query) || line.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				sb.AppendLine(line);
				matchCount++;
			}
		}

		txtLog.Text = sb.ToString();
		if (!string.IsNullOrEmpty(query))
		{
			lblInfo.Text = string.Format("ค้นหาพบ {0} / {1} บรรทัด", matchCount, allLines.Count);
		}
		else
		{
			lblInfo.Text = string.Format("แสดงทั้งหมด {0} บรรทัด (เลื่อนลงเพื่อดูแชทล่าสุด)", allLines.Count);
		}

		// Scroll to bottom to view latest messages
		txtLog.SelectionStart = txtLog.Text.Length;
		txtLog.ScrollToCaret();
	}
}
