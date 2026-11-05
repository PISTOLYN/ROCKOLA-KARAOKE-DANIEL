using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

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
        private readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlexaRockola", "config.ini");
        private string mediaFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        private string marqueeText = "QUIERE CANTAR  QUIERES ESCUCHAR MUSICA";
        private string activeCategory = "MUSICA";
        private readonly List<Genre> genres = new List<Genre>();
        private FlowLayoutPanel genrePanel;
        private ListView songList;
        private ListBox playlist;
        private TextBox searchBox;
        private Label marquee;
        private Label status;
        private Label counters;
        private System.Windows.Forms.Timer marqueeTimer;
        private LibVLC libVLC;
        private MediaPlayer mediaPlayer;
        private Media currentMedia;
        private VideoWindow videoWindow;
        private bool closing;
        private Keys keyMenu = Keys.M, keyAdd = Keys.Enter, keyPause = Keys.Space, keyFullScreen = Keys.F11, keyStop = Keys.Escape;
        private bool rightClickAddsSong = true;

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
            LoadConfig();
            try
            {
                Core.Initialize();
                libVLC = new LibVLC("--no-video-title-show", "--quiet");
                mediaPlayer = new MediaPlayer(libVLC);
                mediaPlayer.EndReached += MediaPlayer_EndReached;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo iniciar el motor multimedia VLC.\r\n" + ex.Message, "ALEXA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            BuildInterface();
            ScanLibrary();
            RefreshSongs();
            RefreshQueue();
            FormClosing += delegate { closing = true; StopPlayback(); SaveConfig(); };
        }

        private void BuildInterface()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = black, Padding = new Padding(5) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 245));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            Controls.Add(root);

            marquee = new Label { Dock = DockStyle.Fill, BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 19, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Text = RepeatMarquee() };
            marquee.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(marquee, 0, 0);

            var albums = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = navy, WrapContents = false, AutoScroll = true, Padding = new Padding(10), Margin = new Padding(0, 8, 0, 8), BorderStyle = BorderStyle.FixedSingle };
            string[] albumNames = { "LEON ROCKOLAS", "MUSICA", "ARTISTAS", "JUAN GABRIEL", "1D KIDS", "KARAOKE", "TIERRA SHOW" };
            for (int i = 0; i < albumNames.Length; i++)
            {
                var tile = new Panel { Width = 205, Height = 210, BackColor = i == 0 ? yellow : Color.FromArgb(18, 30, 70), Margin = new Padding(8, 4, 8, 4), Padding = new Padding(5) };
                if (i == 0)
                {
                    var art = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(40, 34, 28), SizeMode = PictureBoxSizeMode.Zoom };
                    string defaultCoverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "DefaultCover.jpg");
                    if (File.Exists(defaultCoverPath))
                    {
                        try { using (var source = Image.FromFile(defaultCoverPath)) art.Image = new Bitmap(source); }
                        catch { art.BackColor = Color.FromArgb(40, 34, 28); }
                    }
                    tile.Controls.Add(art);
                }
                else
                {
                    var art = new Label { Dock = DockStyle.Fill, BackColor = (i == 1 || i == 5 ? Color.FromArgb(95, 20, 105) : Color.FromArgb(8, 40, 90)), ForeColor = Color.White, Font = new Font("Arial", 20, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, Text = (i == 1 || i == 5) ? "♫" : albumNames[i] };
                    tile.Controls.Add(art);
                }
                albums.Controls.Add(tile);
            }
            root.Controls.Add(albums, 0, 1);

            var topTools = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = cyan, Padding = new Padding(5) };
            topTools.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            topTools.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var searchRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 490));
            searchBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Arial", 15) };
            searchBox.TextChanged += delegate { RefreshSongs(); };
            searchRow.Controls.Add(searchBox, 0, 0);
            counters = new Label { Dock = DockStyle.Fill, BackColor = black, ForeColor = cyan, TextAlign = ContentAlignment.MiddleCenter };
            searchRow.Controls.Add(counters, 1, 0);
            topTools.Controls.Add(searchRow, 0, 0);

            var categories = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = cyan, WrapContents = false, AutoScroll = true };
            genrePanel = categories;
            BuildGenreButtons();
            topTools.Controls.Add(categories, 0, 1);
            root.Controls.Add(topTools, 0, 2);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = black, Padding = new Padding(0, 4, 0, 4) };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = cyan };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            header.Controls.Add(new Label { Text = "[BIBLIOTECA DE CANCIONES]", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Arial", 12, FontStyle.Bold) }, 0, 0);
            header.Controls.Add(new Label { Text = "ARTISTA / CARPETA", Dock = DockStyle.Fill, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter }, 1, 0);
            left.Controls.Add(header, 0, 0);
            songList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = false, BackColor = black, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Arial", 12, FontStyle.Bold), HideSelection = false, MultiSelect = false };
            songList.Columns.Add("CANCIÓN", 690);
            songList.Columns.Add("ARTISTA / CARPETA", 330);
            songList.DoubleClick += delegate { AddSelectedSong(); };
            songList.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Right && rightClickAddsSong) AddSelectedSong(); };
            songList.KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { AddSelectedSong(); e.Handled = true; } };
            left.Controls.Add(songList, 0, 1);
            body.Controls.Add(left, 0, 0);

            var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White, Margin = new Padding(4, 0, 0, 0) };
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            right.Controls.Add(new Label { Text = "MUSICA\r\nLISTA EN REPRODUCCION", Dock = DockStyle.Fill, BackColor = Color.Red, ForeColor = Color.Yellow, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Arial", 11, FontStyle.Bold) }, 0, 0);
            playlist = new ListBox { Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = Color.DarkBlue, Font = new Font("Arial", 11, FontStyle.Bold), IntegralHeight = false };
            playlist.DoubleClick += delegate { PlayQueueSelection(); };
            right.Controls.Add(playlist, 0, 1);
            body.Controls.Add(right, 1, 0);
            root.Controls.Add(body, 0, 3);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = cyan };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 480));
            status = new Label { Dock = DockStyle.Fill, BackColor = Color.Red, ForeColor = Color.Yellow, Font = new Font("Arial", 19, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Text = "NO HAY TEMAS EN ESPERA   NO HAY TEMAS EN ESPERA" };
            bottom.Controls.Add(status, 0, 0);
            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = cyan, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
            AddControl(controls, "◀", delegate { MoveQueue(-1); });
            AddControl(controls, "▶", delegate { PlayQueueSelection(); });
            AddControl(controls, "▼", delegate { MoveQueue(1); });
            AddControl(controls, "▲", delegate { MoveQueue(-1); });
            AddControl(controls, "ABC", delegate { searchBox.Focus(); });
            AddControl(controls, "⌕", delegate { searchBox.Focus(); });
            AddControl(controls, "M", ShowMainMenu);
            bottom.Controls.Add(controls, 1, 0);
            root.Controls.Add(bottom, 0, 4);

            marqueeTimer = new System.Windows.Forms.Timer { Interval = 160 };
            marqueeTimer.Tick += delegate { if (marquee.Text.Length > 1) marquee.Text = marquee.Text.Substring(1) + marquee.Text.Substring(0, 1); };
            marqueeTimer.Start();
        }
        private void ShowMainMenu()
        {
            using (var menu = new Form())
            {
                menu.Text = "MENÚ PRINCIPAL - ALEXA";
                menu.StartPosition = FormStartPosition.CenterParent;
                menu.FormBorderStyle = FormBorderStyle.FixedDialog;
                menu.MaximizeBox = false; menu.MinimizeBox = false;
                menu.Size = new Size(1120, 790);
                menu.BackColor = Color.FromArgb(238, 240, 245);
                var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(22, 14, 22, 14), BackColor = menu.BackColor };
                menu.Controls.Add(panel);
                Action<string, Action> add = delegate(string caption, Action action) {
                    var b = new Button { Text = caption, Width = 1030, Height = 62, BackColor = Color.FromArgb(245, 246, 249), ForeColor = Color.FromArgb(8, 22, 48), FlatStyle = FlatStyle.Flat, Font = new Font("Arial Narrow", 25, FontStyle.Bold), Margin = new Padding(4, 3, 4, 3) };
                    b.FlatAppearance.BorderColor = Color.Silver;
                    b.Click += delegate { menu.Close(); action(); };
                    panel.Controls.Add(b);
                };
                add("CONFIGURAR MÚSICA", ShowMusicSettings);
                add("CONFIGURACIÓN GENERAL", ShowSettings);
                add("SKINS - VISUALIZACIÓN", delegate { MessageBox.Show("La personalización de skins se habilitará en una siguiente etapa.", "ALEXA"); });
                add("CONFIGURAR TECLADO", ShowKeyboardSettings);
                add("ESTADÍSTICAS", delegate { MessageBox.Show("Archivos detectados: " + songs.Count + "\r\nTemas en cola: " + queue.Count, "ESTADÍSTICAS"); });
                add("ACTIVAR PROGRAMA", delegate { MessageBox.Show("ALEXA está ejecutándose.", "ALEXA"); });
                add("CERRAR", delegate { StopPlayback(); });
                add("CERRAR PROGRAMA", delegate { Close(); });
                add("APAGAR PC", delegate { MessageBox.Show("Por seguridad, el apagado del equipo no se ejecuta desde esta versión.", "ALEXA"); });
                add("ACERCA DE...", delegate { MessageBox.Show("ALEXA - LEON ROCKOLAS\r\nRockola y karaoke para Windows.", "ACERCA DE"); });
                add("RECOMENDACIONES", delegate { MessageBox.Show("Organiza tus archivos en carpetas llamadas Música, Karaokes, Videos o con el nombre de cada género.", "ALEXA"); });
                menu.ShowDialog(this);
            }
        }

        private void ShowMusicSettings()
        {
            using (var form = new GenreManagerForm(genres))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    genres.Clear();
                    genres.AddRange(form.Genres);
                    if (!genres.Any(g => g.Name.Equals(activeCategory, StringComparison.OrdinalIgnoreCase))) activeCategory = genres.Count == 0 ? "" : genres[0].Name;
                    BuildGenreButtons();
                    SaveConfig();
                    RefreshSongs();
                }
            }
        }

                private string RepeatMarquee() { return "   " + marqueeText + "       " + marqueeText + "       " + marqueeText + "   "; }

        private void BuildGenreButtons()
        {
            if (genrePanel == null) return;
            genrePanel.Controls.Clear();
            foreach (Genre g in genres)
            {
                var b = new Button { Text = g.Name.ToUpperInvariant(), Width = 140, Height = 48, BackColor = g.Color, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = new Font("Arial", 12, FontStyle.Bold), Margin = new Padding(5, 3, 5, 3) };
                b.FlatAppearance.BorderSize = 0;
                b.Click += delegate { activeCategory = g.Name; RefreshSongs(); };
                genrePanel.Controls.Add(b);
            }
        }

        private void EnsureDefaultGenres()
        {
            if (genres.Count > 0) return;
            genres.Add(new Genre("MUSICA", yellow, "AUDIO"));
            genres.Add(new Genre("KARAOKES", Color.Lime, "KARAOKE"));
            genres.Add(new Genre("VIDEOS", Color.Magenta, "VIDEO"));
        }

        private void AddControl(FlowLayoutPanel panel, string text, Action action)
        {
            var b = new Button { Text = text, Width = 48, Height = 46, BackColor = Color.Gainsboro, ForeColor = Color.Black, Font = new Font("Arial", 15, FontStyle.Bold), Margin = new Padding(3), FlatStyle = FlatStyle.Flat };
            b.Click += delegate { action(); };
            panel.Controls.Add(b);
        }

        private void ScanLibrary()
        {
            songs.Clear();
            if (!Directory.Exists(mediaFolder)) return;
            string[] audio = { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma" };
            string[] video = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".mpeg", ".mpg", ".webm" };
            try
            {
                foreach (string file in Directory.EnumerateFiles(mediaFolder, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (audio.Contains(ext) || video.Contains(ext))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        string folder = new DirectoryInfo(Path.GetDirectoryName(file)).Name;
                        songs.Add(new Song(name, folder, file, video.Contains(ext)));
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("No se pudo leer toda la carpeta:\r\n" + ex.Message, "ALEXA"); }
            songs.Sort((a, b) => String.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase));
        }

        private void RefreshSongs()
        {
            if (songList == null) return;
            string q = searchBox == null ? "" : searchBox.Text.Trim();
            Genre selected = genres.FirstOrDefault(g => g.Name.Equals(activeCategory, StringComparison.OrdinalIgnoreCase));
            IEnumerable<Song> filtered = songs;
            if (selected != null)
            {
                if (selected.Folders.Count > 0) filtered = filtered.Where(s => selected.Folders.Any(folder => IsInsideFolder(s.Path, folder)));
                else if (selected.Kind == "AUDIO") filtered = filtered.Where(s => !s.IsVideo && !IsInNamedFolder(s.Path, "KARAOKE"));
                else if (selected.Kind == "KARAOKE") filtered = filtered.Where(s => IsInNamedFolder(s.Path, "KARAOKE"));
                else if (selected.Kind == "VIDEO") filtered = filtered.Where(s => s.IsVideo && !IsInNamedFolder(s.Path, "KARAOKE"));
                else filtered = filtered.Where(s => IsInNamedFolder(s.Path, selected.Name));
            }
            if (!String.IsNullOrWhiteSpace(q)) filtered = filtered.Where(s => (s.Title + " " + s.Artist).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            songList.BeginUpdate();
            songList.Items.Clear();
            foreach (Song s in filtered)
            {
                var item = new ListViewItem((s.IsVideo ? "▶  " : "■  ") + s.Title);
                item.SubItems.Add(s.Artist);
                item.Tag = s;
                songList.Items.Add(item);
            }
            songList.EndUpdate();
            counters.Text = "ARCHIVOS: " + songs.Count + "  •  MOSTRANDO: " + songList.Items.Count + "  •  " + activeCategory;
            if (songList.Items.Count > 0) songList.Items[0].Selected = true;
        }

        private bool IsInNamedFolder(string path, string folderName)
        {
            string normalized = path.Replace('/', '\\').ToUpperInvariant();
            string token = "\\" + folderName.ToUpperInvariant() + "\\";
            return normalized.Contains(token);
        }

        private bool IsInsideFolder(string filePath, string folderPath)
        {
            try
            {
                string root = System.IO.Path.GetFullPath(folderPath).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
                return System.IO.Path.GetFullPath(filePath).StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

                private void AddSelectedSong()
        {
            if (songList.SelectedItems.Count == 0) return;
            Song s = songList.SelectedItems[0].Tag as Song;
            if (s == null) return;
            queue.Add(s);
            RefreshQueue();
            playlist.SelectedIndex = queue.Count - 1;
        }

        private void AddFolder()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Selecciona una carpeta que contenga música o videos" })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    mediaFolder = dialog.SelectedPath;
                    SaveConfig();
                    ScanLibrary();
                    RefreshSongs();
                }
            }
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

        private void PlayQueueSelection()
        {
            if (mediaPlayer == null) { MessageBox.Show("El motor multimedia no está disponible. Revisa la instalación.", "ALEXA"); return; }
            if (queue.Count == 0) { AddSelectedSong(); if (queue.Count == 0) return; }
            int index = playlist.SelectedIndex < 0 ? 0 : playlist.SelectedIndex;
            PlayAt(index);
        }

        private void PlayAt(int index)
        {
            if (index < 0 || index >= queue.Count || mediaPlayer == null) return;
            Song song = queue[index];
            try
            {
                mediaPlayer.Stop();
                if (currentMedia != null) { currentMedia.Dispose(); currentMedia = null; }
                currentMedia = new Media(libVLC, song.Path, FromType.FromPath);
                mediaPlayer.Play(currentMedia);
                playlist.SelectedIndex = index;
                status.Text = "REPRODUCIENDO: " + song.Title;
                if (song.IsVideo) ShowVideoWindow(); else if (videoWindow != null) videoWindow.Hide();
            }
            catch (Exception ex) { MessageBox.Show("No se pudo reproducir el archivo:\r\n" + ex.Message, "ALEXA"); }
        }

        private void ShowVideoWindow()
        {
            if (videoWindow == null || videoWindow.IsDisposed) videoWindow = new VideoWindow(mediaPlayer);
            videoWindow.Show();
            videoWindow.BringToFront();
        }

        private void MediaPlayer_EndReached(object sender, EventArgs e)
        {
            if (closing || IsDisposed) return;
            try { BeginInvoke((Action)delegate { int next = playlist.SelectedIndex + 1; if (next < queue.Count) PlayAt(next); else { status.Text = "NO HAY TEMAS EN ESPERA"; if (videoWindow != null) videoWindow.Hide(); } }); }
            catch { }
        }

        private void StopPlayback()
        {
            try { if (mediaPlayer != null) mediaPlayer.Stop(); if (currentMedia != null) { currentMedia.Dispose(); currentMedia = null; } if (videoWindow != null) videoWindow.Hide(); } catch { }
        }

        private void ShowKeyboardSettings()
        {
            using (var form = new KeyboardSettingsForm(keyMenu,keyAdd,keyPause,keyFullScreen,keyStop,rightClickAddsSong))
            {
                if(form.ShowDialog(this)==DialogResult.OK){keyMenu=form.MenuKey;keyAdd=form.AddKey;keyPause=form.PauseKey;keyFullScreen=form.FullScreenKey;keyStop=form.StopKey;rightClickAddsSong=form.RightClickAddsSong;SaveConfig();}
            }
        }

        private void ShowSettings()
        {
            using (var form = new SettingsForm(mediaFolder, marqueeText))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    mediaFolder = form.MediaFolder;
                    marqueeText = String.IsNullOrWhiteSpace(form.MarqueeText) ? marqueeText : form.MarqueeText.Trim();
                    SaveConfig();
                    marquee.Text = RepeatMarquee();
                    ScanLibrary();
                    RefreshSongs();
                }
            }
        }

                private void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    foreach (string line in File.ReadAllLines(configPath))
                    {
                        int p = line.IndexOf('=');
                        if (p < 1) continue;
                        string key = line.Substring(0, p);
                        string value = line.Substring(p + 1);
                        if (key == "MediaFolder") mediaFolder = value;
                        else if (key == "MarqueeText") marqueeText = value;
                        else if (key == "KeyMenu") Enum.TryParse(value, out keyMenu);
                        else if (key == "KeyAdd") Enum.TryParse(value, out keyAdd);
                        else if (key == "KeyPause") Enum.TryParse(value, out keyPause);
                        else if (key == "KeyFullScreen") Enum.TryParse(value, out keyFullScreen);
                        else if (key == "KeyStop") Enum.TryParse(value, out keyStop);
                        else if (key == "RightClickAddsSong") Boolean.TryParse(value, out rightClickAddsSong);
                        else if (key == "GenreFolder")
                        {
                            try {
                                string[] fp=value.Split('|');
                                if(fp.Length==2) {
                                    string gn=Encoding.UTF8.GetString(Convert.FromBase64String(fp[0]));
                                    string path=Encoding.UTF8.GetString(Convert.FromBase64String(fp[1]));
                                    Genre target=genres.FirstOrDefault(g=>g.Name.Equals(gn,StringComparison.OrdinalIgnoreCase));
                                    if(target!=null && Directory.Exists(path) && !target.Folders.Contains(path,StringComparer.OrdinalIgnoreCase)) target.Folders.Add(path);
                                }
                            } catch { }
                        }
                        else if (key == "Genre")
                        {
                            string[] parts = value.Split('|');
                            if (parts.Length >= 5)
                            {
                                int r,g,b;
                                if (Int32.TryParse(parts[2], out r) && Int32.TryParse(parts[3], out g) && Int32.TryParse(parts[4], out b))
                                    genres.Add(new Genre(parts[0], Color.FromArgb(r,g,b), parts[1]));
                            }
                        }
                    }
                }
            }
            catch { }
            EnsureDefaultGenres();
            if (String.IsNullOrEmpty(activeCategory) || !genres.Any(g => g.Name == activeCategory)) activeCategory = genres[0].Name;
        }

        private void SaveConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(configPath);
                Directory.CreateDirectory(dir);
                var lines = new List<string> { "MediaFolder=" + mediaFolder, "MarqueeText=" + marqueeText, "KeyMenu="+keyMenu, "KeyAdd="+keyAdd, "KeyPause="+keyPause, "KeyFullScreen="+keyFullScreen, "KeyStop="+keyStop, "RightClickAddsSong="+rightClickAddsSong };
                foreach (Genre g in genres)
                {
                    lines.Add("Genre=" + g.Name.Replace("|","") + "|" + g.Kind + "|" + g.Color.R + "|" + g.Color.G + "|" + g.Color.B);
                    foreach(string folder in g.Folders) lines.Add("GenreFolder="+Convert.ToBase64String(Encoding.UTF8.GetBytes(g.Name))+"|"+Convert.ToBase64String(Encoding.UTF8.GetBytes(folder)));
                }
                File.WriteAllLines(configPath, lines);
            }
            catch { }
        }

                private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == keyMenu) { ShowMainMenu(); e.Handled = true; }
            if (e.KeyCode == keyAdd && songList.Focused) { AddSelectedSong(); e.Handled = true; }
            if (e.KeyCode == keyPause && mediaPlayer != null) { mediaPlayer.Pause(); e.Handled = true; }
            if (e.KeyCode == keyStop) { if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal; else StopPlayback(); e.Handled = true; }
            if (e.KeyCode == keyFullScreen) { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; e.Handled = true; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (marqueeTimer != null) marqueeTimer.Dispose(); if (mediaPlayer != null) { mediaPlayer.EndReached -= MediaPlayer_EndReached; mediaPlayer.Dispose(); } if (currentMedia != null) currentMedia.Dispose(); if (libVLC != null) libVLC.Dispose(); if (videoWindow != null) videoWindow.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        private class Song
        {
            public string Title, Artist, Path;
            public bool IsVideo;
            public Song(string title, string artist, string path, bool isVideo) { Title = title; Artist = artist; Path = path; IsVideo = isVideo; }
        }
    }

    internal class Genre
    {
        public string Name; public Color Color; public string Kind;
        public List<string> Folders = new List<string>();
        public Genre(string name, Color color, string kind) { Name = name; Color = color; Kind = kind; }
    }

    internal class GenreManagerForm : Form
    {
        private List<Genre> working;
        private ListBox genreList, folderList;
        private TextBox nameBox;
        private ComboBox kindBox, colorBox;
        public List<Genre> Genres { get { return working; } }

        public GenreManagerForm(List<Genre> current)
        {
            working=current.Select(g=>{var n=new Genre(g.Name,g.Color,g.Kind);n.Folders.AddRange(g.Folders);return n;}).ToList();
            Text="CONFIGURAR MÚSICA Y GÉNEROS"; Size=new Size(850,590); MinimumSize=new Size(760,520);
            StartPosition=FormStartPosition.CenterParent; FormBorderStyle=FormBorderStyle.Sizable;
            var tabs=new TabControl{Dock=DockStyle.Fill}; Controls.Add(tabs);
            var genresTab=new TabPage("GÉNEROS-ARTISTAS"); var foldersTab=new TabPage("CARPETAS"); tabs.TabPages.Add(genresTab);tabs.TabPages.Add(foldersTab);
            var grid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),ColumnCount=2,RowCount=5};
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,38));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,38));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,38));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            genresTab.Controls.Add(grid);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
            Button add=Btn("AGREGAR",145),del=Btn("BORRAR",145),edit=Btn("MODIFICAR",145),refresh=Btn("ACTUALIZAR",145);
            actions.Controls.Add(add);actions.Controls.Add(del);actions.Controls.Add(edit);actions.Controls.Add(refresh);grid.Controls.Add(actions,0,0);
            genreList=new ListBox{Dock=DockStyle.Fill,Font=new Font("Arial",11,FontStyle.Bold)};grid.Controls.Add(genreList,1,0);
            grid.Controls.Add(new Label{Text="NOMBRE DEL GÉNERO",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,1);
            nameBox=new TextBox{Dock=DockStyle.Fill};grid.Controls.Add(nameBox,1,1);
            grid.Controls.Add(new Label{Text="TIPO DE ARCHIVO",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,2);
            kindBox=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};kindBox.Items.AddRange(new object[]{"AUDIO","KARAOKE","VIDEO","CARPETA"});kindBox.SelectedIndex=0;grid.Controls.Add(kindBox,1,2);
            grid.Controls.Add(new Label{Text="COLOR DEL BOTÓN",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,3);
            colorBox=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};colorBox.Items.AddRange(new object[]{"AMARILLO","VERDE","ROSA","AZUL","NARANJA","CELESTE"});colorBox.SelectedIndex=0;grid.Controls.Add(colorBox,1,3);
            Button save=Btn("GUARDAR CAMBIOS",220);save.BackColor=Color.LimeGreen;grid.Controls.Add(save,1,4);
            add.Click+=delegate{AddGenre();};del.Click+=delegate{DeleteGenre();};edit.Click+=delegate{ModifyGenre();};refresh.Click+=delegate{RefreshGenres();RefreshFolders();};
            genreList.SelectedIndexChanged+=delegate{LoadSelected();};save.Click+=delegate{DialogResult=DialogResult.OK;Close();};RefreshGenres();

            var folderGrid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=3};
            folderGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,44));folderGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));folderGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,48));foldersTab.Controls.Add(folderGrid);
            folderGrid.Controls.Add(new Label{Text="Selecciona un género en la primera pestaña y agrega las carpetas que pertenecen a ese género.",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
            folderList=new ListBox{Dock=DockStyle.Fill,Font=new Font("Arial",10)};folderGrid.Controls.Add(folderList,0,1);
            var folderActions=new FlowLayoutPanel{Dock=DockStyle.Fill};
            Button addFolder=Btn("AGREGAR CARPETA",175),addSub=Btn("AGREGAR SUBCARPETA",195),removeFolder=Btn("QUITAR CARPETA",175);
            folderActions.Controls.Add(addFolder);folderActions.Controls.Add(addSub);folderActions.Controls.Add(removeFolder);folderGrid.Controls.Add(folderActions,0,2);
            addFolder.Click+=delegate{PickFolder();};addSub.Click+=delegate{PickFolder();};
            removeFolder.Click+=delegate{Genre g=Selected();int i=folderList.SelectedIndex;if(g!=null&&i>=0){g.Folders.RemoveAt(i);RefreshFolders();}};
            tabs.SelectedIndexChanged+=delegate{LoadSelected();};
        }
        private Button Btn(string text,int width){return new Button{Text=text,Width=width,Height=34,Margin=new Padding(3),Font=new Font("Arial",9,FontStyle.Bold)};}
        private Genre Selected(){int i=genreList.SelectedIndex;return i>=0&&i<working.Count?working[i]:null;}
        private void AddGenre(){string n=nameBox.Text.Trim();if(n.Length==0||working.Any(g=>g.Name.Equals(n,StringComparison.OrdinalIgnoreCase))){MessageBox.Show("Escribe un nombre nuevo y único.");return;}working.Add(new Genre(n,SelectedColor(),kindBox.SelectedItem.ToString()));RefreshGenres();genreList.SelectedIndex=working.Count-1;nameBox.Clear();}
        private void DeleteGenre(){Genre g=Selected();if(g==null)return;if(new[]{"MUSICA","KARAOKES","VIDEOS"}.Contains(g.Name.ToUpperInvariant())){MessageBox.Show("Los géneros principales no se pueden borrar.");return;}working.Remove(g);RefreshGenres();}
        private void ModifyGenre(){Genre g=Selected();if(g==null)return;string n=nameBox.Text.Trim();if(n.Length==0)n=g.Name;if(working.Any(x=>x!=g&&x.Name.Equals(n,StringComparison.OrdinalIgnoreCase))){MessageBox.Show("Ese nombre ya existe.");return;}g.Name=n;g.Kind=kindBox.SelectedItem.ToString();g.Color=SelectedColor();RefreshGenres();}
        private void PickFolder(){Genre g=Selected();if(g==null){MessageBox.Show("Primero selecciona un género en GÉNEROS-ARTISTAS.");return;}using(var d=new FolderBrowserDialog{Description="Selecciona la carpeta o subcarpeta que pertenece a "+g.Name})if(d.ShowDialog(this)==DialogResult.OK&&!g.Folders.Contains(d.SelectedPath,StringComparer.OrdinalIgnoreCase)){g.Folders.Add(d.SelectedPath);RefreshFolders();}}
        private void LoadSelected(){Genre g=Selected();if(g==null)return;nameBox.Text=g.Name;kindBox.SelectedItem=g.Kind;colorBox.SelectedIndex=ColorIndex(g.Color);RefreshFolders();}
        private int ColorIndex(Color c){if(c.ToArgb()==Color.LimeGreen.ToArgb())return 1;if(c.ToArgb()==Color.HotPink.ToArgb())return 2;if(c.ToArgb()==Color.RoyalBlue.ToArgb())return 3;if(c.ToArgb()==Color.DarkOrange.ToArgb())return 4;if(c.ToArgb()==Color.Cyan.ToArgb())return 5;return 0;}
        private Color SelectedColor(){switch(colorBox.SelectedIndex){case 0:return Color.FromArgb(255,205,0);case 1:return Color.LimeGreen;case 2:return Color.HotPink;case 3:return Color.RoyalBlue;case 4:return Color.DarkOrange;default:return Color.Cyan;}}
        private void RefreshGenres(){int old=genreList.SelectedIndex;genreList.Items.Clear();foreach(Genre g in working)genreList.Items.Add(g.Name+" ("+g.Kind+")");if(genreList.Items.Count>0)genreList.SelectedIndex=Math.Max(0,Math.Min(old,genreList.Items.Count-1));}
        private void RefreshFolders(){if(folderList==null)return;folderList.Items.Clear();Genre g=Selected();if(g!=null)foreach(string p in g.Folders)folderList.Items.Add(p);}
    }

    internal class KeyboardSettingsForm : Form
    {
        public Keys MenuKey { get; private set; }
        public Keys AddKey { get; private set; }
        public Keys PauseKey { get; private set; }
        public Keys FullScreenKey { get; private set; }
        public Keys StopKey { get; private set; }
        public bool RightClickAddsSong { get; private set; }
        private TextBox[] keyBoxes = new TextBox[5];
        private Keys[] selectedKeys = new Keys[5];
        private CheckBox rightClick;
        public KeyboardSettingsForm(Keys menuKey, Keys addKey, Keys pauseKey, Keys fullKey, Keys stopKey, bool rightAdds)
        {
            Text="CONFIGURAR TECLAS"; Size=new Size(850,560); StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            selectedKeys=new Keys[]{menuKey,addKey,pauseKey,fullKey,stopKey};
            var tabs=new TabControl{Dock=DockStyle.Fill}; Controls.Add(tabs);
            var keyTab=new TabPage("CONFIGURAR TECLAS"); var otherTab=new TabPage("OTROS"); tabs.TabPages.Add(keyTab);tabs.TabPages.Add(otherTab);
            var grid=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=2,RowCount=5};
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
            string[] names={"ABRIR MENÚ PRINCIPAL","AGREGAR CANCIÓN A LA LISTA","PAUSAR / CONTINUAR","PANTALLA COMPLETA","DETENER / RESTAURAR"};
            for(int i=0;i<5;i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute,66));
                grid.Controls.Add(new Label{Text=names[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Arial",12,FontStyle.Bold)},0,i);
                int index=i; keyBoxes[i]=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Text=selectedKeys[i].ToString(),TextAlign=HorizontalAlignment.Center,Font=new Font("Arial",13,FontStyle.Bold),BackColor=Color.White};
                keyBoxes[i].Click+=delegate{keyBoxes[index].Text="PRESIONA UNA TECLA...";keyBoxes[index].Focus();};
                keyBoxes[i].KeyDown+=delegate(object sender,KeyEventArgs e){selectedKeys[index]=e.KeyCode;keyBoxes[index].Text=e.KeyCode.ToString();e.SuppressKeyPress=true;e.Handled=true;};
                grid.Controls.Add(keyBoxes[i],1,i);
            }
            keyTab.Controls.Add(grid);
            rightClick=new CheckBox{Text="EL CLIC DERECHO AGREGA LA CANCIÓN SELECCIONADA A LA LISTA",Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(24),Checked=rightAdds,Font=new Font("Arial",11,FontStyle.Bold)};
            otherTab.Controls.Add(rightClick);
            var bottom=new Panel{Dock=DockStyle.Bottom,Height=64};Controls.Add(bottom);
            var save=new Button{Text="GUARDAR CAMBIOS",Width=230,Dock=DockStyle.Right,BackColor=Color.LimeGreen,Font=new Font("Arial",12,FontStyle.Bold)};
            save.Click+=delegate{MenuKey=selectedKeys[0];AddKey=selectedKeys[1];PauseKey=selectedKeys[2];FullScreenKey=selectedKeys[3];StopKey=selectedKeys[4];RightClickAddsSong=rightClick.Checked;DialogResult=DialogResult.OK;Close();};
            bottom.Controls.Add(save);
        }
    }

    internal class SettingsForm : Form
    {
        private TextBox folderBox, marqueeBox;
        public string MediaFolder { get; private set; }
        public string MarqueeText { get; private set; }

        public SettingsForm(string folder, string marquee)
        {
            Text = "CONFIGURACIÓN ALEXA";
            Size = new Size(650, 250);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            BackColor = Color.FromArgb(5, 17, 43); ForeColor = Color.White;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text = "CARPETA PRINCIPAL DE MÚSICA Y KARAOKE", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            folderBox = new TextBox { Dock = DockStyle.Fill, Text = folder };
            var browse = new Button { Text = "EXAMINAR", Dock = DockStyle.Fill };
            browse.Click += delegate { using (var d = new FolderBrowserDialog()) if (d.ShowDialog(this) == DialogResult.OK) folderBox.Text = d.SelectedPath; };
            folderRow.Controls.Add(folderBox, 0, 0); folderRow.Controls.Add(browse, 1, 0);
            layout.Controls.Add(folderRow, 0, 1);
            layout.Controls.Add(new Label { Text = "TEXTO DE MARQUESINA", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            marqueeBox = new TextBox { Dock = DockStyle.Fill, Text = marquee };
            var save = new Button { Text = "GUARDAR CAMBIOS", Dock = DockStyle.Fill, BackColor = Color.Lime, ForeColor = Color.Black };
            save.Click += delegate { MediaFolder = folderBox.Text.Trim(); MarqueeText = marqueeBox.Text.Trim(); DialogResult = DialogResult.OK; Close(); };
            bottom.Controls.Add(marqueeBox, 0, 0); bottom.Controls.Add(save, 1, 0);
            layout.Controls.Add(bottom, 0, 3);
        }
    }

    internal class VideoWindow : Form
    {
        private VideoView view;
        public VideoWindow(MediaPlayer player)
        {
            Text = "ALEXA - VIDEO / KARAOKE";
            BackColor = Color.Black;
            WindowState = FormWindowState.Maximized;
            view = new VideoView { Dock = DockStyle.Fill, MediaPlayer = player, BackColor = Color.Black };
            Controls.Add(view);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; }
            base.OnFormClosing(e);
        }
    }
}