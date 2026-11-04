using System;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Windows.Forms;
using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;
using RockolaKaraoke.Servicios;

namespace RockolaKaraoke.Interfaz
{
    public class FormularioReproductor : Form
    {
        private readonly ListView biblioteca = new ListView();
        private readonly FlowLayoutPanel mosaico = new FlowLayoutPanel();
        private readonly TextBox busquedaMosaico = new TextBox();
        private string filtroTipo = "Todos";
        private readonly ListView cola = new ListView();
        private readonly Label etiquetaCola = new Label();
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
            Load += delegate { ConfiguracionSkin.Aplicar(this); };
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
            foreach (Button b in new Button[] { anterior, reproducir, siguiente, detener }) { b.Size = new Size(145, 43); Estilizar(b); controles.Controls.Add(b); }
            Label etiquetaVolumen = new Label { Text = "VOLUMEN", Width = 75, Height = 40, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White };
            volumen.Minimum = 0; volumen.Maximum = 100; volumen.Value = 70; volumen.Width = 130; volumen.Height = 42; volumen.TickFrequency = 10;
            controles.Controls.Add(etiquetaVolumen); controles.Controls.Add(volumen);
            anterior.Click += delegate { ReproducirAnterior(); };
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
            biblioteca.Dock = DockStyle.None; biblioteca.Visible = false;
            biblioteca.View = View.Details; biblioteca.FullRowSelect = true;
            biblioteca.Columns.Add("CANCIÓN", 190); biblioteca.Columns.Add("ARTISTA", 125);
            mosaico.Dock = DockStyle.Fill; mosaico.AutoScroll = true; mosaico.WrapContents = true;
            mosaico.FlowDirection = FlowDirection.LeftToRight; mosaico.Padding = new Padding(14);
            mosaico.BackColor = Color.FromArgb(12, 16, 31);
            busquedaMosaico.Dock = DockStyle.Top; busquedaMosaico.Height = 38;
            busquedaMosaico.Font = new Font("Segoe UI", 13F); busquedaMosaico.Margin = new Padding(12);
            busquedaMosaico.ForeColor = Color.DimGray; busquedaMosaico.Text = "Buscar canción o artista...";
            busquedaMosaico.GotFocus += delegate { if (busquedaMosaico.Text == "Buscar canción o artista...") { busquedaMosaico.Text = ""; busquedaMosaico.ForeColor = Color.Black; } };
            busquedaMosaico.LostFocus += delegate { if (String.IsNullOrWhiteSpace(busquedaMosaico.Text)) { busquedaMosaico.Text = "Buscar canción o artista..."; busquedaMosaico.ForeColor = Color.DimGray; } };
            busquedaMosaico.TextChanged += delegate { ConstruirMosaico(); };

            Panel lado = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(12, 16, 31) };
            FlowLayoutPanel filtros = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 5, 8, 4), WrapContents = false };
            foreach (string tipo in new string[] { "Todos", "Musica", "Video", "Karaoke" })
            {
                Button filtro = new Button { Text = tipo == "Musica" ? "MÚSICA" : tipo.ToUpperInvariant(), Tag = tipo, Width = 105, Height = 36 };
                Estilizar(filtro);
                filtro.Click += delegate(object sender, EventArgs e) { filtroTipo = Convert.ToString(((Button)sender).Tag); ConstruirMosaico(); };
                filtros.Controls.Add(filtro);
            }
            Button actualizar = new Button { Text = "ACTUALIZAR BIBLIOTECA", Dock = DockStyle.Top, Height = 42 };
            Estilizar(actualizar); actualizar.Click += delegate { CargarBiblioteca(); };
            Button agregar = new Button { Text = "＋ AGREGAR A LA COLA", Dock = DockStyle.Top, Height = 42 };
            Estilizar(agregar); agregar.Click += delegate { AgregarSeleccionALaCola(); };
            Button ahora = new Button { Text = "▶ REPRODUCIR AHORA", Dock = DockStyle.Top, Height = 42 };
            Estilizar(ahora); ahora.Click += delegate { ReproducirSeleccion(); };
            lado.Controls.Add(mosaico); lado.Controls.Add(filtros); lado.Controls.Add(busquedaMosaico);
            lado.Controls.Add(agregar); lado.Controls.Add(ahora); lado.Controls.Add(actualizar); lado.Controls.Add(biblioteca);

            Panel panelDerecho = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            Panel panelCola = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = Color.FromArgb(20, 25, 44) };
            etiquetaCola.Text = "COLA DE REPRODUCCIÓN (0)";
            etiquetaCola.Dock = DockStyle.Top; etiquetaCola.Height = 28; etiquetaCola.TextAlign = ContentAlignment.MiddleLeft;
            etiquetaCola.Padding = new Padding(10, 0, 0, 0); etiquetaCola.ForeColor = Color.DeepSkyBlue;
            cola.Dock = DockStyle.Fill; cola.View = View.Details; cola.FullRowSelect = true; cola.GridLines = false;
            cola.HideSelection = false; cola.BackColor = Color.FromArgb(20, 25, 44); cola.ForeColor = Color.White;
            cola.Columns.Add("SIGUIENTE CANCIÓN", 300); cola.Columns.Add("ARTISTA", 190);
            cola.DoubleClick += delegate { ReproducirElementoDeCola(); };

            FlowLayoutPanel accionesCola = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 39, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(5, 2, 5, 2) };
            Button quitar = new Button { Text = "QUITAR", Width = 95, Height = 32 };
            Button subir = new Button { Text = "SUBIR ▲", Width = 105, Height = 32 };
            Button bajar = new Button { Text = "BAJAR ▼", Width = 105, Height = 32 };
            Button vaciar = new Button { Text = "VACIAR COLA", Width = 115, Height = 32 };
            foreach (Button b in new Button[] { quitar, subir, bajar, vaciar }) { Estilizar(b); accionesCola.Controls.Add(b); }
            quitar.Click += delegate { QuitarDeLaCola(); };
            subir.Click += delegate { MoverEnCola(-1); };
            bajar.Click += delegate { MoverEnCola(1); };
            vaciar.Click += delegate { cola.Items.Clear(); ActualizarContadorCola(); };

            panelCola.Controls.Add(cola);
            panelCola.Controls.Add(accionesCola);
            panelCola.Controls.Add(etiquetaCola);
            pantalla.Dock = DockStyle.Fill;
            pantalla.BackColor = Color.Black;
            panelDerecho.Controls.Add(pantalla);
            panelDerecho.Controls.Add(panelCola);
            division.Panel1.Controls.Add(lado);
            division.Panel2.Controls.Add(panelDerecho);

            Controls.Add(division); Controls.Add(inferior); Controls.Add(encabezado);
        }

        private void ConstruirMosaico()
        {
            mosaico.SuspendLayout();
            mosaico.Controls.Clear();
            string texto = busquedaMosaico.Text == "Buscar canción o artista..." ? "" : busquedaMosaico.Text.Trim();

            foreach (ListViewItem elemento in biblioteca.Items)
            {
                string tipo = Convert.ToString(elemento.ToolTipText);
                if (filtroTipo != "Todos" && !tipo.Equals(filtroTipo, StringComparison.OrdinalIgnoreCase)) continue;
                if (texto.Length > 0 && elemento.Text.IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0 &&
                    elemento.SubItems[1].Text.IndexOf(texto, StringComparison.OrdinalIgnoreCase) < 0) continue;

                // Tarjeta cuadrada provisional: posteriormente se conectará la portada real del álbum.
                Panel tarjeta = new Panel
                {
                    Width = 218,
                    Height = 218,
                    Margin = new Padding(10),
                    BackColor = Color.FromArgb(17, 23, 42),
                    Cursor = Cursors.Hand,
                    Tag = elemento
                };
                tarjeta.Paint += delegate(object sender, PaintEventArgs e)
                {
                    Panel p = (Panel)sender;
                    using (Pen borde = new Pen(Color.FromArgb(35, 145, 220), 2F))
                        e.Graphics.DrawRectangle(borde, 1, 1, p.Width - 3, p.Height - 3);
                };

                Panel portada = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(22, 55, 95),
                    Cursor = Cursors.Hand
                };
                portada.Paint += delegate(object sender, PaintEventArgs e)
                {
                    Panel p = (Panel)sender;
                    Rectangle area = p.ClientRectangle;
                    using (System.Drawing.Drawing2D.LinearGradientBrush fondo = new System.Drawing.Drawing2D.LinearGradientBrush(
                        area, Color.FromArgb(8, 30, 65), Color.FromArgb(75, 24, 105), 40F))
                        e.Graphics.FillRectangle(fondo, area);

                    using (System.Drawing.Drawing2D.LinearGradientBrush brillo = new System.Drawing.Drawing2D.LinearGradientBrush(
                        area, Color.FromArgb(0, 0, 190, 255), Color.FromArgb(85, 0, 190, 255), 90F))
                        e.Graphics.FillEllipse(brillo, -35, -55, 180, 180);

                    string simbolo = tipo.Equals("Video", StringComparison.OrdinalIgnoreCase) ? "▶" : "♫";
                    using (Font fuente = new Font("Segoe UI Symbol", 66F, FontStyle.Bold))
                    using (SolidBrush pincel = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                    {
                        SizeF medida = e.Graphics.MeasureString(simbolo, fuente);
                        e.Graphics.DrawString(simbolo, fuente, pincel,
                            (area.Width - medida.Width) / 2F, (area.Height - medida.Height) / 2F - 12F);
                    }
                };

                Panel franja = new Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 64,
                    BackColor = Color.FromArgb(235, 7, 12, 27),
                    Cursor = Cursors.Hand
                };
                Label nombre = new Label
                {
                    Text = elemento.Text,
                    Dock = DockStyle.Top,
                    Height = 36,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoEllipsis = true,
                    Padding = new Padding(5, 0, 5, 0),
                    Cursor = Cursors.Hand
                };
                Label artista = new Label
                {
                    Text = elemento.SubItems[1].Text,
                    Dock = DockStyle.Fill,
                    ForeColor = Color.FromArgb(80, 205, 255),
                    Font = new Font("Segoe UI", 8.5F),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoEllipsis = true,
                    Cursor = Cursors.Hand
                };
                franja.Controls.Add(artista);
                franja.Controls.Add(nombre);

                Button sumar = new Button
                {
                    Text = "+",
                    Width = 34,
                    Height = 34,
                    Top = 7,
                    Left = 174,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(0, 155, 220),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 17F, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    TabStop = false
                };
                sumar.FlatAppearance.BorderSize = 0;
                sumar.Click += delegate { SeleccionarElemento(elemento); AgregarSeleccionALaCola(); };

                EventHandler reproducirTarjeta = delegate { SeleccionarElemento(elemento); ReproducirSeleccion(); };
                tarjeta.Click += reproducirTarjeta;
                portada.Click += reproducirTarjeta;
                franja.Click += reproducirTarjeta;
                nombre.Click += reproducirTarjeta;
                artista.Click += reproducirTarjeta;

                tarjeta.Controls.Add(portada);
                tarjeta.Controls.Add(franja);
                tarjeta.Controls.Add(sumar);
                mosaico.Controls.Add(tarjeta);
            }

            mosaico.ResumeLayout();
        }

        private void SeleccionarElemento(ListViewItem elemento)
        {
            LimpiarSeleccionBiblioteca();
            elemento.Selected = true;
            elemento.Focused = true;
            elemento.EnsureVisible();
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
                ConstruirMosaico();
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
            if (indice < 0 || indice >= biblioteca.Items.Count) return;
            ListViewItem elemento = biblioteca.Items[indice];
            ReproducirArchivo(Convert.ToString(elemento.Tag), elemento.Text, elemento.SubItems[1].Text);
            if (biblioteca.Items[indice].Selected)
            {
                biblioteca.Items[indice].EnsureVisible();
            }
            else
            {
                LimpiarSeleccionBiblioteca();
                biblioteca.Items[indice].Selected = true;
                biblioteca.Items[indice].EnsureVisible();
            }
        }

        private void ReproducirArchivo(string ruta, string titulo, string artista)
        {
            if (reproductor == null) { MessageBox.Show(this, "El motor multimedia no está disponible. Revisa la instalación.", "Reproductor"); return; }
            if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta)) { MessageBox.Show(this, "El archivo ya no está disponible:\n" + ruta, "Archivo no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            try
            {
                reproductor.Stop();
                if (medioActual != null) medioActual.Dispose();
                medioActual = new Media(motor, new Uri(Path.GetFullPath(ruta)));
                if (!reproductor.Play(medioActual)) throw new InvalidOperationException("VLC no pudo iniciar la reproducción.");
                tituloActual.Text = titulo + "  —  " + artista;
                reproducir.Text = "Ⅱ PAUSAR";
            }
            catch (Exception ex) { MessageBox.Show(this, "No se pudo reproducir el archivo.\n" + ex.Message, "Error de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void AgregarSeleccionALaCola()
        {
            if (biblioteca.SelectedIndices.Count == 0)
            {
                MessageBox.Show(this, "Primero selecciona una canción o video de la biblioteca.", "Cola de reproducción", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ListViewItem origen = biblioteca.SelectedItems[0];
            ListViewItem nuevo = new ListViewItem(origen.Text);
            nuevo.SubItems.Add(origen.SubItems[1].Text);
            nuevo.Tag = Convert.ToString(origen.Tag);
            cola.Items.Add(nuevo);
            ActualizarContadorCola();
        }

        private void ReproducirElementoDeCola()
        {
            if (cola.SelectedIndices.Count == 0) return;
            int indice = cola.SelectedIndices[0];
            ListViewItem elemento = cola.Items[indice];
            string ruta = Convert.ToString(elemento.Tag);
            string titulo = elemento.Text;
            string artista = elemento.SubItems[1].Text;
            cola.Items.RemoveAt(indice);
            ActualizarContadorCola();
            ReproducirArchivo(ruta, titulo, artista);
            SeleccionarEnBiblioteca(ruta);
        }

        private void QuitarDeLaCola()
        {
            if (cola.SelectedIndices.Count == 0) return;
            cola.Items.RemoveAt(cola.SelectedIndices[0]);
            ActualizarContadorCola();
        }

        private void MoverEnCola(int desplazamiento)
        {
            if (cola.SelectedIndices.Count == 0) return;
            int indice = cola.SelectedIndices[0];
            int destino = indice + desplazamiento;
            if (destino < 0 || destino >= cola.Items.Count) return;
            ListViewItem elemento = (ListViewItem)cola.Items[indice].Clone();
            cola.Items.RemoveAt(indice);
            cola.Items.Insert(destino, elemento);
            elemento.Selected = true;
            elemento.EnsureVisible();
            ActualizarContadorCola();
        }

        private void ActualizarContadorCola()
        {
            etiquetaCola.Text = "COLA DE REPRODUCCIÓN (" + cola.Items.Count + ")";
        }

        private void LimpiarSeleccionBiblioteca()
        {
            foreach (ListViewItem elemento in biblioteca.Items)
                elemento.Selected = false;
        }

        private void SeleccionarEnBiblioteca(string ruta)
        {
            LimpiarSeleccionBiblioteca();
            for (int i = 0; i < biblioteca.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(biblioteca.Items[i].Tag), ruta, StringComparison.OrdinalIgnoreCase))
                {
                    biblioteca.Items[i].Selected = true;
                    biblioteca.Items[i].EnsureVisible();
                    return;
                }
            }
        }

        private void ReproducirAnterior()
        {
            if (biblioteca.SelectedIndices.Count == 0) return;
            int indice = biblioteca.SelectedIndices[0] - 1;
            if (indice < 0) indice = biblioteca.Items.Count - 1;
            ReproducirIndice(indice);
        }

        private void ReproducirSiguiente()
        {
            if (cola.Items.Count > 0)
            {
                ListViewItem elemento = cola.Items[0];
                string ruta = Convert.ToString(elemento.Tag);
                string titulo = elemento.Text;
                string artista = elemento.SubItems[1].Text;
                cola.Items.RemoveAt(0);
                ActualizarContadorCola();
                ReproducirArchivo(ruta, titulo, artista);
                SeleccionarEnBiblioteca(ruta);
                return;
            }

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
            long segundosTotales = Math.Max(0L, milisegundos) / 1000L;
            long horas = segundosTotales / 3600L;
            long minutos = (segundosTotales % 3600L) / 60L;
            long segundos = segundosTotales % 60L;
            if (horas > 0)
                return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", horas, minutos, segundos);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", minutos, segundos);
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