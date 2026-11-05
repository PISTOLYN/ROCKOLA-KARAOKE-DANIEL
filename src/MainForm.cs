using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AlexaRockola
{
    public class MainForm : Form
    {
        private readonly Color cyan = Color.FromArgb(0, 205, 225);
        private readonly Color navy = Color.FromArgb(5, 17, 43);
        private readonly Color black = Color.FromArgb(2, 5, 15);
        private readonly Color yellow = Color.FromArgb(255, 196, 0);
        private readonly List<Song> songs = new List<Song>();
        private readonly List<Song> queue = new List<Song>();
        private ListView songList;
        private ListBox playlist;
        private TextBox searchBox;
        private Label marquee;
        private Label status;
        private Label counters;
        private System.Windows.Forms.Timer marqueeTimer;
        private int marqueeOffset;

        public MainForm()
        {
            Text = "ALEXA - LEON ROCKOLAS";
            BackColor = black;
            ForeColor = Color.White;
            Font = new Font("Arial", 11, FontStyle.Bold);
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1100, 700);
            KeyPreview = true;
            KeyDown += MainForm_KeyDown;
            SeedSongs();
            BuildInterface();
            RefreshSongs();
            RefreshQueue();
        }

        private void BuildInterface()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = black, Padding = new Padding(5) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(root);

            marquee = new Label { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 19, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Text = "   QUIERE CANTAR  QUIERES ESCUCHA MUSICA     QUIERE CANTAR  QUIERES ESCUCHA MUSICA     QUIERE CANTAR  QUIERES ESCUCHA MUSICA   " };
            marquee.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(marquee, 0, 0);

            var albums = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = navy, WrapContents = false, AutoScroll = true, Padding = new Padding(10), Margin = new Padding(0, 8, 0, 8), BorderStyle = BorderStyle.FixedSingle };
            string[] albumNames = { "LEON ROCKOLAS", "MUSICA", "ARTISTAS", "JUAN GABRIEL", "1D KIDS", "KARAOKE", "TIERRA SHOW" };
            for (int i = 0; i < albumNames.Length; i++)
            {
                var tile = new Panel { Width = 205, Height = 235, BackColor = i == 0 ? yellow : Color.FromArgb(18, 30, 70), Margin = new Padding(8, 4, 8, 4), Padding = new Padding(5) };
                var art = new Label { Dock = DockStyle.Fill, BackColor = i == 0 ? Color.FromArgb(40, 34, 28) : (i == 1 || i == 5 ? Color.FromArgb(95, 20, 105) : Color.FromArgb(8, 40, 90)), ForeColor = Color.White, Font = new Font("Arial", i == 0 ? 17 : 20, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Text = i == 0 ? "♌\r\nLEON\r\nROCKOLAS" : (i == 1 || i == 5 ? "♫" : albumNames[i]) };
                tile.Controls.Add(art);
                albums.Controls.Add(tile);
            }
            root.Controls.Add(albums, 0, 1);

            var topTools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = cyan, Padding = new Padding(5) };
            topTools.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            topTools.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 490));
            searchBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Arial", 15), Text = "" };
            searchBox.TextChanged += delegate { RefreshSongs(); };
            searchRow.Controls.Add(searchBox, 0, 0);
            counters = new Label { Dock = DockStyle.Fill, BackColor = black, ForeColor = cyan, TextAlign = ContentAlignment.MiddleCenter, Text = "MINIATURAS DE ÁLBUMES  •  7 DE 54  •  GÉNERO 2 DE 4" };
            searchRow.Controls.Add(counters, 1, 0);
            topTools.Controls.Add(searchRow, 0, 0);

            var categories = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = cyan, WrapContents = false };
            AddCategory(categories, "MUSICA", yellow, Color.RoyalBlue);
            AddCategory(categories, "KARAOKES", Color.Lime, Color.Blue);
            AddCategory(categories, "VIDEOS", Color.Magenta, Color.Yellow);
            AddCategory(categories, "", Color.FromArgb(0, 100, 220), Color.White);
            AddCategory(categories, "", Color.FromArgb(230, 130, 0), Color.White);
            AddCategory(categories, "", Color.FromArgb(235, 55, 180), Color.White);
            AddCategory(categories, "", Color.FromArgb(65, 215, 230), Color.White);
            AddCategory(categories, "", Color.Gainsboro, Color.Black);
            topTools.Controls.Add(categories, 0, 1);
            root.Controls.Add(topTools, 0, 2);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = black, Padding = new Padding(0, 4, 0, 4) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 225));
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = cyan };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            header.Controls.Add(new Label { Text = "[ÁLBUM SELECCIONADO]  •  1 DE 15", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Arial", 12, FontStyle.Bold) }, 0, 0);
            header.Controls.Add(new Label { Text = "ARTISTA", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
            left.Controls.Add(header, 0, 0);
            songList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, BackColor = black, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Arial", 12, FontStyle.Bold), HideSelection = false };
            songList.Columns.Add("CANCIÓN", 690);
            songList.Columns.Add("ARTISTA / CARPETA", 330);
            songList.DoubleClick += delegate { AddSelectedSong(); };
            songList.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { AddSelectedSong(); e.Handled = true; } };
            left.Controls.Add(songList, 0, 1);
            body.Controls.Add(left, 0, 0);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White, Margin = new Padding(4, 0, 0, 0) };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.Controls.Add(new Label { Text = "MUSICA\r\nLISTA EN REPRODUCCION", Dock = DockStyle.Fill, BackColor = Color.Red, ForeColor = Color.Yellow, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 11, FontStyle.Bold) }, 0, 0);
            playlist = new ListBox { Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = Color.DarkBlue, Font = new Font("Arial", 13, FontStyle.Bold), IntegralHeight = false };
            right.Controls.Add(playlist, 0, 1);
            body.Controls.Add(right, 1, 0);
            root.Controls.Add(body, 0, 3);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = cyan };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 430));
            status = new Label { Dock = DockStyle.Fill, BackColor = Color.Red, ForeColor = Color.Yellow, Font = new Font("Arial", 21, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Text = "NO HAY TEMAS EN ESPERA   NO HAY TEMAS EN ESPERA" };
            bottom.Controls.Add(status, 0, 0);
            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = cyan, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
            AddControl(controls, "◀", delegate { MoveQueue(-1); });
            AddControl(controls, "▶", delegate { PlaySelected(); });
            AddControl(controls, "▼", delegate { MoveQueue(1); });
            AddControl(controls, "▲", delegate { MoveQueue(-1); });
            AddControl(controls, "ABC", delegate { searchBox.Focus(); });
            AddControl(controls, "⌕", delegate { searchBox.Focus(); });
            bottom.Controls.Add(controls, 1, 0);
            root.Controls.Add(bottom, 0, 4);

            marqueeTimer = new System.Windows.Forms.Timer { Interval = 180 };
            marqueeTimer.Tick += delegate
            {
                string t = marquee.Text;
                if (t.Length > 1) { marqueeOffset = (marqueeOffset + 1) % t.Length; marquee.Text = t.Substring(1) + t.Substring(0, 1); }
            };
            marqueeTimer.Start();
        }

        private void AddCategory(FlowLayoutPanel panel, string text, Color back, Color fore)
        {
            var b = new Button { Text = text, Width = 140, Height = 48, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 14, FontStyle.Bold), Margin = new Padding(7, 3, 7, 3) };
            b.FlatAppearance.BorderSize = 0;
            b.Click += delegate { if (text.Length > 0) counters.Text = "CATEGORÍA: " + text + "  •  MINIATURAS DE ÁLBUMES 7 DE 54"; };
            panel.Controls.Add(b);
        }

        private void AddControl(FlowLayoutPanel panel, string text, Action action)
        {
            var b = new Button { Text = text, Width = 48, Height = 46, BackColor = Color.Gainsboro, ForeColor = Color.Black, Font = new Font("Arial", 15, FontStyle.Bold), Margin = new Padding(4, 3, 4, 3), FlatStyle = FlatStyle.Flat };
            b.Click += delegate { action(); };
            panel.Controls.Add(b);
        }

        private void SeedSongs()
        {
            songs.Add(new Song("Banda Los Recoditos - Me Esta Tirando El Rollo 2016", "+bandas 2016"));
            songs.Add(new Song("Calibre 50 - Prestamela A Mi (Historias De La Calle)", "+bandas 2016"));
            songs.Add(new Song("Cuanto Te Costo - Calibre 50", "+bandas 2016"));
            songs.Add(new Song("La Sonora Santanera - Bomboro Quiña Quiña", "+bomboro"));
            songs.Add(new Song("Juan Gabriel y Emmanuel - Pero Que Necesidad", "+juan gabriel 2016"));
            songs.Add(new Song("Luis Mario - Obsesion", "+Luis Mario"));
            songs.Add(new Song("ARIEL CAMACHO REY DE CORAZONES", "+mp3 2016"));
            songs.Add(new Song("Cada Quien - Ariel Camacho", "+mp3 2016"));
            songs.Add(new Song("Hablemos - Ariel Camacho", "+mp3 2016"));
        }

        private void RefreshSongs()
        {
            if (songList == null) return;
            string q = searchBox == null ? "" : searchBox.Text.Trim();
            songList.BeginUpdate();
            songList.Items.Clear();
            foreach (Song s in songs.Where(x => (x.Title + " " + x.Artist).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                var item = new ListViewItem("■  " + s.Title);
                item.SubItems.Add(s.Artist);
                item.Tag = s;
                songList.Items.Add(item);
            }
            songList.EndUpdate();
            if (songList.Items.Count > 0) songList.Items[0].Selected = true;
        }

        private void AddSelectedSong()
        {
            if (songList.SelectedItems.Count == 0) return;
            Song s = songList.SelectedItems[0].Tag as Song;
            if (s == null) return;
            queue.Add(s);
            RefreshQueue();
        }

        private void RefreshQueue()
        {
            if (playlist == null) return;
            playlist.Items.Clear();
            for (int i = 0; i < queue.Count; i++) playlist.Items.Add((i + 1) + ": " + queue[i].Title);
            status.Text = queue.Count == 0 ? "NO HAY TEMAS EN ESPERA   NO HAY TEMAS EN ESPERA" : "TEMAS EN ESPERA: " + queue.Count + "   •   LEON ROCKOLAS";
        }

        private void MoveQueue(int delta)
        {
            int i = playlist.SelectedIndex;
            if (i < 0 || i >= queue.Count) return;
            int j = Math.Max(0, Math.Min(queue.Count - 1, i + delta));
            if (i == j) return;
            Song s = queue[i]; queue.RemoveAt(i); queue.Insert(j, s);
            RefreshQueue(); playlist.SelectedIndex = j;
        }

        private void PlaySelected()
        {
            if (queue.Count == 0) { AddSelectedSong(); return; }
            MessageBox.Show("La cola está lista. El motor de reproducción multimedia se integrará en la siguiente etapa.", "ALEXA", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && songList.Focused) AddSelectedSong();
            if (e.KeyCode == Keys.Escape) WindowState = FormWindowState.Normal;
            if (e.KeyCode == Keys.F11) WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }

        private class Song
        {
            public string Title; public string Artist;
            public Song(string title, string artist) { Title = title; Artist = artist; }
        }
    }
}