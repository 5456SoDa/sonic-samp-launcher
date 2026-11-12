using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal class ViewerForm : Form
{
	private List<string> imagePaths = new List<string>();
	private int currentIndex = 0;
	private PictureBox pb;
	private Label lblInfo;
	private Panel topBar;

	public ViewerForm(string path) : this(path, null)
	{
	}

	public ViewerForm(string path, List<string> allPaths)
	{
		base.Text = "แกลเลอรีภาพหน้าจอ (SA-MP Screenshots)";
		base.BackColor = Color.FromArgb(10, 10, 16);
		base.StartPosition = FormStartPosition.CenterParent;
		base.Size = new Size(1000, 680);
		base.ShowInTaskbar = false;
		base.KeyPreview = true;
		base.DoubleBuffered = true;

		if (allPaths != null && allPaths.Count > 0)
		{
			imagePaths.AddRange(allPaths);
			currentIndex = imagePaths.IndexOf(path);
			if (currentIndex < 0) currentIndex = 0;
		}
		else
		{
			imagePaths.Add(path);
			currentIndex = 0;
		}

		// Top Controls Bar
		topBar = new Panel
		{
			Dock = DockStyle.Top,
			Height = 44,
			BackColor = Color.FromArgb(16, 22, 34),
			Padding = new Padding(12, 6, 12, 6)
		};

		lblInfo = new Label
		{
			Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(51, 204, 255),
			AutoSize = true,
			Location = new Point(14, 11)
		};
		topBar.Controls.Add(lblInfo);

		RoundedBtn btnPrev = new RoundedBtn("◀ ก่อนหน้า", Color.FromArgb(28, 42, 66), Color.FromArgb(40, 60, 95), Color.White, 6, new Font("Segoe UI", 9f))
		{
			Size = new Size(90, 30),
			Location = new Point(base.Width - 430, 7),
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		btnPrev.Click += delegate { ShowPrev(); };
		topBar.Controls.Add(btnPrev);

		RoundedBtn btnNext = new RoundedBtn("ถัดไป ▶", Color.FromArgb(28, 42, 66), Color.FromArgb(40, 60, 95), Color.White, 6, new Font("Segoe UI", 9f))
		{
			Size = new Size(80, 30),
			Location = new Point(base.Width - 334, 7),
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		btnNext.Click += delegate { ShowNext(); };
		topBar.Controls.Add(btnNext);

		RoundedBtn btnCopy = new RoundedBtn("📋 คัดลอก", Color.FromArgb(28, 42, 66), Color.FromArgb(40, 60, 95), Color.FromArgb(255, 235, 0), 6, new Font("Segoe UI", 9f))
		{
			Size = new Size(84, 30),
			Location = new Point(base.Width - 248, 7),
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		btnCopy.Click += delegate { CopyCurrentImage(); };
		topBar.Controls.Add(btnCopy);

		RoundedBtn btnClose = new RoundedBtn("✕ ปิด (Esc)", Color.FromArgb(50, 20, 26), Color.FromArgb(80, 25, 35), Color.FromArgb(255, 120, 120), 6, new Font("Segoe UI", 9f))
		{
			Size = new Size(88, 30),
			Location = new Point(base.Width - 158, 7),
			Anchor = AnchorStyles.Top | AnchorStyles.Right
		};
		btnClose.Click += delegate { Close(); };
		topBar.Controls.Add(btnClose);

		base.Controls.Add(topBar);

		// Picture Box
		pb = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.FromArgb(10, 10, 16),
			Cursor = Cursors.Hand
		};
		pb.Click += delegate
		{
			// Clicking picture advances to next image or closes if last
			if (currentIndex < imagePaths.Count - 1)
			{
				ShowNext();
			}
			else
			{
				Close();
			}
		};
		base.Controls.Add(pb);
		pb.BringToFront();

		base.KeyDown += ViewerForm_KeyDown;

		base.FormClosed += delegate
		{
			ClearCurrentImage();
		};

		LoadCurrentImage();
	}

	private void ClearCurrentImage()
	{
		if (pb.Image != null)
		{
			Image old = pb.Image;
			pb.Image = null;
			try { old.Dispose(); } catch { }
		}
	}

	private void LoadCurrentImage()
	{
		if (currentIndex < 0 || currentIndex >= imagePaths.Count) return;

		string curPath = imagePaths[currentIndex];
		if (!File.Exists(curPath)) return;

		ClearCurrentImage();

		try
		{
			// Load via byte[] to avoid file locking on disk
			byte[] fileBytes = File.ReadAllBytes(curPath);
			using (MemoryStream ms = new MemoryStream(fileBytes))
			{
				pb.Image = new Bitmap(Image.FromStream(ms));
			}

			string fileName = Path.GetFileName(curPath);
			lblInfo.Text = string.Format("🖼️  {0}  ({1}/{2})", fileName, currentIndex + 1, imagePaths.Count);
			base.Text = string.Format("{0} - SA-MP Screenshot Viewer ({1}/{2})", fileName, currentIndex + 1, imagePaths.Count);
		}
		catch (Exception ex)
		{
			lblInfo.Text = "เปิดรูปภาพไม่สำเร็จ: " + ex.Message;
		}
	}

	private void ShowPrev()
	{
		if (imagePaths.Count <= 1) return;
		currentIndex--;
		if (currentIndex < 0) currentIndex = imagePaths.Count - 1;
		LoadCurrentImage();
	}

	private void ShowNext()
	{
		if (imagePaths.Count <= 1) return;
		currentIndex++;
		if (currentIndex >= imagePaths.Count) currentIndex = 0;
		LoadCurrentImage();
	}

	private void CopyCurrentImage()
	{
		if (pb.Image != null)
		{
			try
			{
				Clipboard.SetImage(pb.Image);
				string prevText = lblInfo.Text;
				lblInfo.Text = "✅ คัดลอกรูปภาพลง Clipboard เรียบร้อยแล้ว!";
				lblInfo.ForeColor = Color.FromArgb(102, 255, 0);

				Timer t = new Timer { Interval = 2000 };
				t.Tick += delegate
				{
					t.Stop();
					t.Dispose();
					if (!base.IsDisposed)
					{
						lblInfo.Text = prevText;
						lblInfo.ForeColor = Color.FromArgb(51, 204, 255);
					}
				};
				t.Start();
			}
			catch { }
		}
	}

	private void ViewerForm_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape)
		{
			e.SuppressKeyPress = true;
			Close();
		}
		else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A || e.KeyCode == Keys.PageUp)
		{
			e.SuppressKeyPress = true;
			ShowPrev();
		}
		else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D || e.KeyCode == Keys.PageDown || e.KeyCode == Keys.Space)
		{
			e.SuppressKeyPress = true;
			ShowNext();
		}
		else if (e.Control && e.KeyCode == Keys.C)
		{
			e.SuppressKeyPress = true;
			CopyCurrentImage();
		}
	}
}
