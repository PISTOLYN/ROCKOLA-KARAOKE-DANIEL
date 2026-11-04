using System;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace RockolaKaraoke.Interfaz
{
    public class FormularioReproductor : Form
    {
        private readonly ListView biblioteca = new ListView();
        private readonly VideoView pantalla = new VideoView();
        private readonly Label tituloActual = new Label();
        private readonly Label tiempo = new Label();
        private readonly TrackBar progreso = new TrackBar();
        private readonly TrackBar volumen = new TrackBar();
        private readonly Button reproducir = new Button();
        private readonly Button anterior = new Button();
        private readonly Button siguiente = new Button();
        private readonly Button detener = new Button();
        private readonly Timer reloj = new Timer();
        private LibVLC motor;
        private MediaPlayer reproductor;
        private Media medioActual;
        private bool moviendoProgreso;

        public FormularioReproductor()
        {
            Text = "Reproductor multimedia - Rockola Karaoke Daniel";
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1050, 680);
            BackColor = Color.FromArgb(12, 16, 31);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            CrearInterfaz();
            Load += delegate { InicializarMotor(); CargarBiblioteca(); };
            FormClosing += delegate { LiberarMotor(); };
            reloj.Interval = 500;
            reloj.Tick += Reloj_Tick;
        }

        private void CrearInterfaz()
        {
            Panel encabezado = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.FromArgb(23, 30, 53) };
            Label marca = new Label { Text = "ROCKOLA  •  REPRODUCTOR MULTIMEDIA", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.DeepSkyBlue, Font = new Font("Segoe UI", 20F, FontStyle.Bold) };
            encabezado.Controls.Add(marca);

            Panel inferior = new Panel { Dock = DockStyle.Bottom, Height = 145, BackColor = Color.FromArgb(20, 25, 44) };
            tituloActual.Text = "Selecciona una canción de la biblioteca";
            tituloActual.Dock = DockStyle.Top; tituloActual.Height = 30; tituloActual.TextAlign = ContentAlignment.MiddleCenter;
            tituloActual.ForeColor = Color.White; tituloActual.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

            progreso.Dock = DockStyle.Top; progreso.Minimum = 0; progreso.Maximum = 1000; progreso.TickStyle = TickStyle.None; progreso.Height = 28;
            progreso.Scroll += delegate { if (reproductor != null && reproductor.IsSeekable && reproductor.Length > 0) reproductor.Position = progreso.Value / 1000f; };
            tiempo.Text = "00:00 / 00:00"; tiempo.Dock = DockStyle.Top; tiempo.Height = 20; tiempo.TextAlign = ContentAlignment.MiddleCenter; tiempo.ForeColor = Color.LightGray;

            FlowLayoutPanel controles = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 58, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(12, 5, 12, 3), BackColor = Color.FromArgb(20, 25, 44) };
            anterior.Text = "⏮ ANTERIOR"; reproducir.Text = "▶ REPRODUCIR"; siguiente.Text = "SIGUIENTE ⏭"; detener.Text = "■ DETENER";
            foreach (Button b in new Button[] { anterior, reproducir, siguiente, detener }) { b.Size = new Size(155, 43); Estilizar(b); controles.Controls.Add(b); }
            Label etiquetaVolumen = new Label { Text = "VOLUMEN", Width = 85, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White };
            volumen.Minimum = 0; volumen.Maximum = 100; volumen.Value = 70; volumen.Width = 150; volumen.Height = 42; volumen.TickFrequency = 10;
            controles.Controls.Add(etiquetaVolumen); controles.Controls.Add(volumen);
            anterior.Click += delegate { ReproducirIndice(biblioteca.SelectedIndices.Count > 0 ? biblioteca.SelectedIndices[0] - 1 : 0); };
            siguiente.Click += delegate { ReproducirSiguiente(); };
            reproducir.Click += delegate
            {
                if (reproductor != null && reproductor.State == VLCState.Playing)
                {
                    reproductor.Pause();
                    reproducir.Text = "▶ CONTINUAR";
                }
                else if (reproductor != null && reproductor.State == VLCState.Paused)
                {
                    reproductor.Pause();
                    reproducir.Text = "Ⅱ PAUSAR";
                }
                else
                {
                    ReproducirSeleccion();
                }
            };
            detener.Click += delegate { if (reproductor != null) reproductor.Stop(); reproducir.Text = "▶ REPRODUCIR"; };
            volumen.Scroll += delegate { if (reproductor != null) reproductor.Volume = volumen.Value; };

            inferior.Controls.Add(controles); inferior.Controls.Add(tiempo); inferior.Controls.Add(progreso); inferior.Controls.Add(tituloActual);

            SplitContainer division = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 340, BackColor = Color.FromArgb(12, 16, 31), FixedPanel = FixedPanel.Panel1 };
            biblioteca.Dock = DockStyle.Fill; biblioteca.View = View.Details; biblioteca.FullRowSelect = true; biblioteca.GridLines = false;
            biblioteca.HideSelection = false; biblioteca.BackColor = Color.FromArgb(20, 25, 44); biblioteca.ForeColor = Color.White;
            biblioteca.Columns.Add("CANCIÓN", 190); biblioteca.Columns.Add("ARTISTA", 125);
            biblioteca.DoubleClick += delegate { ReproducirSeleccion(); };
            Panel lado = new Panel { Dock = DockStyle.Fill };
            Button actualizar = new Button { Text = "ACTUALIZAR BIBLIOTECA", Dock = DockStyle.Top, Height = 45 };
            Estilizar(actualizar); actualizar.Click += delegate { CargarBiblioteca(); };
            lado.Controls.Add(biblioteca); lado.Controls.Add(actualizar);
            pantalla.Dock = DockStyle.Fill; pantalla.BackColor = Color.Black;
            division.Panel1.Controls.Add(lado); division.Panel2.Controls.Add(pantalla);

            Controls.Add(division); Controls.Add(inferior); Controls.Add(encabezado);
        }

        private static void Estilizar(Button boton)
        {
            boton.FlatStyle = FlatStyle.Flat; boton.FlatAppearance.BorderSize = 0;
            boton.BackColor = Color.FromArgb(30, 105, 170); boton.ForeColor = Color.White;
            boton.Font = new Font("Segoe UI", 9F, FontStyle.Bold); boton.Cursor = Cursors.Hand;
        }

        private void InicializarMotor()
        {
            try
            {
                string carpetaVlc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VLC", Environment.Is64BitProcess ? "x64" : "x86");
                if (!File.Exists(Path.Combine(carpetaVlc, "libvlc.dll")))
                    throw new FileNotFoundException("No se encontró el motor VLC de " + (Environment.Is64BitProcess ? "64" : "32") + " bits. Revisa que la carpeta VLC esté junto al programa.", carpetaVlc);
                Core.Initialize(carpetaVlc);
                motor = new LibVLC("--no-video-title-show", "--quiet");
                reproductor = new MediaPlayer(motor);
                pantalla.MediaPlayer = reproductor;
                reproductor.EndReached += delegate
                {
                    if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)ReproducirSiguiente);
                };
                reproductor.EncounteredError += delegate
                {
                    if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)delegate { tituloActual.Text = "No se pudo reproducir este archivo. Revisa su formato o ubicación."; });
                };
                reproductor.Volume = volumen.Value;
                reloj.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "No se pudo iniciar el reproductor multimedia.\n\n" + ex.Message, "Motor multimedia", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CargarBiblioteca()
        {
            biblioteca.Items.Clear();
            try
            {
                using (SQLiteConnection conexion = Datos.BaseDatos.CrearConexion())
                using (SQLiteCommand comando = new SQLiteCommand(
                    "SELECT c.Titulo, IFNULL(a.Nombre,'Sin artista'), c.RutaArchivo, c.Tipo FROM Canciones c LEFT JOIN Artistas a ON a.Id=c.ArtistaId WHERE c.Activa=1 ORDER BY a.Nombre,c.Titulo;", conexion))
                {
                    conexion.Open();
                    using (SQLiteDataReader lector = comando.ExecuteReader())
                    {
                        while (lector.Read())
                        {
                            ListViewItem elemento = new ListViewItem(Convert.ToString(lector[0]));
                            elemento.SubItems.Add(Convert.ToString(lector[1]));
                            elemento.Tag = Convert.ToString(lector[2]);
                            elemento.ToolTipText = Convert.ToString(lector[3]);
                            biblioteca.Items.Add(elemento);
                        }
                    }
                }
                tituloActual.Text = "Biblioteca cargada: " + biblioteca.Items.Count + " canciones y videos";
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo cargar la biblioteca.\n" + ex.Message, "Biblioteca", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ReproducirSeleccion()
        {
            if (biblioteca.SelectedIndices.Count == 0) return;
            ReproducirIndice(biblioteca.SelectedIndices[0]);
        }

        private void ReproducirIndice(int indice)
        {
            if (reproductor == null) { MessageBox.Show(this, "El motor multimedia no está disponible. Revisa la instalación.", "Reproductor"); return; }
            if (indice < 0 || indice >= biblioteca.Items.Count) return;
            ListViewItem elemento = biblioteca.Items[indice];
            string ruta = Convert.ToString(elemento.Tag);
            if (!File.Exists(ruta)) { MessageBox.Show(this, "El archivo ya no está disponible:\n" + ruta, "Archivo no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                reproductor.Stop();
                if (medioActual != null) medioActual.Dispose();
                medioActual = new Media(motor, new Uri(Path.GetFullPath(ruta)));
                if (!reproductor.Play(medioActual)) throw new InvalidOperationException("VLC no pudo iniciar la reproducción.");
                biblioteca.Items[indice].Selected = true;
                biblioteca.Items[indice].EnsureVisible();
                tituloActual.Text = elemento.Text + "  —  " + elemento.SubItems[1].Text;
                reproducir.Text = "Ⅱ PAUSAR";
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo reproducir el archivo.\n" + ex.Message, "Error de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ReproducirSiguiente()
        {
            if (biblioteca.Items.Count == 0) return;
            int indice = biblioteca.SelectedIndices.Count > 0 ? biblioteca.SelectedIndices[0] + 1 : 0;
            if (indice >= biblioteca.Items.Count) indice = 0;
            ReproducirIndice(indice);
        }

        private void Reloj_Tick(object sender, EventArgs e)
        {
            if (reproductor == null || !reproductor.IsPlaying) return;
            if (reproductor.Length > 0)
            {
                moviendoProgreso = true;
                progreso.Value = Math.Max(0, Math.Min(1000, (int)(reproductor.Position * 1000)));
                moviendoProgreso = false;
                tiempo.Text = FormatearTiempo(reproductor.Time) + " / " + FormatearTiempo(reproductor.Length);
            }
        }

        private static string FormatearTiempo(long milisegundos)
        {
            TimeSpan t = TimeSpan.FromMilliseconds(Math.Max(0, milisegundos));
            return t.TotalHours >= 1 ? t.ToString(@"h:mm:ss") : t.ToString(@"mm:ss");
        }

        private void LiberarMotor()
        {
            reloj.Stop();
            if (reproductor != null) { reproductor.Stop(); reproductor.Dispose(); reproductor = null; }
            if (medioActual != null) { medioActual.Dispose(); medioActual = null; }
            if (motor != null) { motor.Dispose(); motor = null; }
        }
    }
}