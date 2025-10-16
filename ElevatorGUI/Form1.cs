using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MongoDB.Bson;

namespace ElevatorGUI
{
    public class Form1 : Form
    {
        // Config
        private readonly int _floorCount = 4;   // tweak to any N ≥ 2

        // Context (state pattern)
        private ElevatorContext _ctx = null!;

        // Logger
        private readonly AsyncMongoLogger _logger = new();

        // UI dynamic
        private readonly List<Button> _hallButtons = new();
        private readonly List<Button> _carButtons = new();
        private readonly List<Label> _floorLabels = new();

        private Panel pnlShaft = null!;
        private Panel pnlCar = null!;
        private Label lblPanel = null!;
        private Label lblTrip = null!;

        private Button btnToggleDb = null!;
        private Button btnRefreshDb = null!;
        private DataGridView dgvDb = null!;

        private Button btnToggleText = null!;
        private TextBox txtLog = null!;
        private readonly List<string> _inMem = new();

        public Form1()
        {
            Text = "Elevator – Tasks 1–5 (State Pattern • BackgroundWorker • Robust)";
            MinimumSize = new Size(1200, 720);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();
            BuildElevatorContext();
            WireContextEvents();
            SeedStartLog();
        }

        private void BuildUi()
        {
            // ---------- grid ----------
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                ColumnCount = 5,
                RowCount = _floorCount + 3, // floors + [log toggle] + [log box] + [spacer/DB row]
            };

            // Column widths: Hall(180) | Car(140) | Floor label(240) | Control panel(220) | Shaft(260)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f)); // 0
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f)); // 1
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240f)); // 2
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f)); // 3
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));  // 4 (grow)

            // Floor rows — fixed height for neat alignment
            for (int r = 0; r < _floorCount; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));

            // Row for "Show Text Log" button
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            // Row for TextBox (log area)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            // Row for DB buttons at the bottom
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

            // ---------- create shared controls ----------
            lblPanel = new Label
            {
                Text = "Floor: 1\nStatus: Idle",
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Consolas", 11f),
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 8, 0)
            };

            lblTrip = new Label
            {
                Text = "Last trip: —",
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 24,
                Margin = new Padding(8, 4, 8, 0)
            };

            // Shaft panel on the right
            pnlShaft = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 0, 0, 0)
            };

            // Car inside shaft
            pnlCar = new Panel
            {
                Size = new Size(54, 54),
                BackColor = Color.SteelBlue
            };
            // Add car after the shaft is in the grid (below)

            // ---------- per-floor rows (top to bottom: F_N .. F_1) ----------
            _hallButtons.Clear();
            _carButtons.Clear();
            _floorLabels.Clear();

            for (int idx = 0; idx < _floorCount; idx++)
            {
                int floor = _floorCount - idx; // row 0 = top floor

                // Hall Request
                var btnHall = new Button
                {
                    Text = $"Request F{floor}",
                    Dock = DockStyle.Fill,
                    Tag = floor,
                    Margin = new Padding(0, 0, 8, 8)
                };
                btnHall.Click += (_, __) => OnRequestFloor((int)btnHall.Tag!, "Hall");
                _hallButtons.Add(btnHall);
                grid.Controls.Add(btnHall, 0, idx);

                // Car Panel
                var btnCar = new Button
                {
                    Text = $"Car → F{floor}",
                    Dock = DockStyle.Fill,
                    Tag = floor,
                    Margin = new Padding(0, 0, 8, 8)
                };
                btnCar.Click += (_, __) => OnRequestFloor((int)btnCar.Tag!, "CarPanel");
                _carButtons.Add(btnCar);
                grid.Controls.Add(btnCar, 1, idx);

                // Floor indicator
                var lbl = new Label
                {
                    Text = $"F{floor}: here? {(floor == 1 ? "YES" : "NO")}",
                    BorderStyle = BorderStyle.FixedSingle,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Tag = floor,
                    Margin = new Padding(0, 0, 8, 8),
                    BackColor = floor == 1 ? Color.Honeydew : SystemColors.Control
                };
                _floorLabels.Add(lbl);
                grid.Controls.Add(lbl, 2, idx);
            }

            // ---------- control panel (span all floor rows) ----------
            // Put the main status panel in column 3, spanning the floor rows
            grid.Controls.Add(lblPanel, 3, 0);
            grid.SetRowSpan(lblPanel, _floorCount);

            // Last trip just below the status (still column 3, next row)
            grid.Controls.Add(lblTrip, 3, _floorCount);

            // ---------- shaft (span all floor rows and the log button row for height) ----------
            grid.Controls.Add(pnlShaft, 4, 0);
            grid.SetRowSpan(pnlShaft, _floorCount + 1); // floors + log toggle row for a bit more height

            // Now that pnlShaft exists, center the car horizontally & place at bottom (F1). Position vertically later too.
            pnlShaft.Controls.Add(pnlCar);
            pnlShaft.Resize += (_, __) =>
            {
                // center X
                pnlCar.Left = (pnlShaft.ClientSize.Width - pnlCar.Width) / 2;
            };

            // ---------- log toggle row ----------
            btnToggleText = new Button
            {
                Text = "Show Text Log",
                Dock = DockStyle.Left,
                Width = 160,
                Margin = new Padding(0, 8, 0, 8)
            };
            btnToggleText.Click += (_, __) =>
            {
                txtLog.Visible = !txtLog.Visible;
                btnToggleText.Text = txtLog.Visible ? "Hide Text Log" : "Show Text Log";
                if (txtLog.Visible) RefreshTextLog();
            };
            // place toggle spanning columns 0..2 (Hall+Car+Labels)
            var toggleHost = new Panel { Dock = DockStyle.Fill };
            toggleHost.Controls.Add(btnToggleText);
            grid.Controls.Add(toggleHost, 0, _floorCount);
            grid.SetColumnSpan(toggleHost, 3);

            // ---------- text log (row below), spans left side columns ----------
            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Visible = false,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10f),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 8, 8)
            };
            grid.Controls.Add(txtLog, 0, _floorCount + 1);
            grid.SetColumnSpan(txtLog, 4); // cover Hall+Car+Labels+ControlPanel

            // ---------- DB controls bottom row (under shaft) ----------
            var dbButtonsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0),
                Margin = new Padding(8, 0, 0, 0)
            };

            btnToggleDb = new Button { Text = "Show DB Logs", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            btnToggleDb.Click += (_, __) =>
            {
                dgvDb.Visible = !dgvDb.Visible;
                btnToggleDb.Text = dgvDb.Visible ? "Hide DB Logs" : "Show DB Logs";
                if (dgvDb.Visible) LoadDbGrid();
            };

            btnRefreshDb = new Button { Text = "Refresh DB", AutoSize = true };
            btnRefreshDb.Click += (_, __) => { if (dgvDb.Visible) LoadDbGrid(); };

            dbButtonsRow.Controls.Add(btnToggleDb);
            dbButtonsRow.Controls.Add(btnRefreshDb);
            grid.Controls.Add(dbButtonsRow, 4, _floorCount + 2);

            // ---------- DB grid (sits above the buttons inside the shaft column) ----------
            dgvDb = new DataGridView
            {
                Visible = false,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Dock = DockStyle.Fill,
                Margin = new Padding(8, 8, 0, 8)
            };
            grid.Controls.Add(dgvDb, 4, _floorCount + 1);

            // ---------- add grid to form ----------
            Controls.Clear();
            Controls.Add(grid);
        }

        private void BuildElevatorContext()
        {
            // Map floor -> Y pixels (1..N). Floor 1 bottom, floor N top, evenly spaced.
            var floorY = new List<int>(_floorCount + 1) { 0 }; // dummy index 0
            int topPad = 12, bottomPad = 12;
            int usable = pnlShaft.Height - topPad - bottomPad - pnlCar.Height;
            for (int f = 1; f <= _floorCount; f++)
            {
                double t = (double)(f - 1) / (_floorCount - 1); // 0..1 bottom->top
                int y = bottomPad + (int)Math.Round(usable * (1 - t));
                floorY.Add(y);
            }

            _ctx = new ElevatorContext(_floorCount, floorY, startFloor: 1, initial: new IdleState())
            {
                SpeedPx = 8
            };
        }

        private void WireContextEvents()
        {
            _ctx.StatusChanged += (_, status) =>
            {
                SafeUi(() => lblPanel.Text = $"Floor: {GetCurrentFloorText()}\nStatus: {status}");
                AppendText(status);
                EnqueueLog("Status", GetCurrentFloor(), status, null);
            };

            _ctx.CarMoved += (_, y) => SafeUi(() => pnlCar.Top = y);

            _ctx.FloorAligned += (_, floor) =>
            {
                SafeUi(() => UpdateFloorLabels(floor));
                EnqueueLog("FloorAligned", floor, $"Aligned with F{floor}", null);
            };

            _ctx.TripCompletedMs += (_, ms) =>
            {
                double s = ms / 1000.0;
                SafeUi(() => lblTrip.Text = $"Last trip: {s:F2} s");
                EnqueueLog("Trip", GetCurrentFloor(), $"Completed trip in {s:F2}s", ms);
            };
        }

        private void SeedStartLog()
        {
            AppendText("System started. Elevator at Floor 1.");
            EnqueueLog("Start", 1, "System started", null);
        }

        private void OnRequestFloor(int floor, string source)
        {
            try
            {
                _ctx.Request(floor, source);
                EnqueueLog(source, floor, $"Requested floor {floor}", null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Request failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // UI helpers
        private void UpdateFloorLabels(int here)
        {
            foreach (var lbl in _floorLabels)
            {
                int f = (int)lbl.Tag!;
                lbl.Text = $"F{f}: here? {(f == here ? "YES" : "NO")}";
                lbl.BackColor = f == here ? Color.Honeydew : SystemColors.Control;
            }
        }

        private int GetCurrentFloor()
        {
            int idx = 1; int best = int.MaxValue;
            for (int f = 1; f <= _floorCount; f++)
            {
                int d = Math.Abs(_ctx.CarY - _ctx.FloorY[f]);
                if (d < best) { best = d; idx = f; }
            }
            return idx;
        }
        private string GetCurrentFloorText() => GetCurrentFloor().ToString();

        private void AppendText(string line)
        {
            _inMem.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
            if (txtLog.Visible) RefreshTextLog();
        }
        private void RefreshTextLog()
        {
            txtLog.Lines = _inMem.ToArray();
            txtLog.SelectionStart = txtLog.TextLength;
            txtLog.ScrollToCaret();
        }
        private void SafeUi(Action a)
        {
            if (InvokeRequired) BeginInvoke(a);
            else a();
        }

        private void EnqueueLog(string evt, int floor, string status, int? ms)
        {
            try { _logger.Enqueue(new LogEntry { EventType = evt, Floor = floor, Status = status, TravelMs = ms }); }
            catch { /* keep running even if enqueue fails */ }
        }

        private void LoadDbGrid()
        {
            try
            {
                var docs = _logger.GetAllLogsSafe();
                var list = docs.Select(d => new
                {
                    Time = d.Contains("Timestamp") ? d["Timestamp"].ToUniversalTime().ToLocalTime() : (DateTime?)null,
                    Event = d.Contains("EventType") ? d["EventType"].AsString : "",
                    Floor = d.Contains("Floor") ? d["Floor"].ToInt32() : 0,
                    Status = d.Contains("Status") ? d["Status"].AsString : "",
                    TravelMs = d.Contains("TravelMs") ? d["TravelMs"].ToInt32() : (int?)null
                }).ToList();
                dgvDb.DataSource = list;
            }
            catch (Exception ex)
            {
                dgvDb.DataSource = new[] { new { Time = DateTime.Now, Event = "ERROR", Floor = 0, Status = ex.Message, TravelMs = (int?)null } };
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            pnlCar.Top = _ctx.FloorY[1];
            UpdateFloorLabels(1);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _logger.Dispose();
            base.OnFormClosed(e);
        }
    }
}
