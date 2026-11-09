using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RockolaKaraoke.Interfaz
{
    /// <summary>
    /// Letrero LED: el texto se repite y camina de IZQUIERDA a DERECHA sin saltos.
    /// </summary>
    public class LetreroLed : Control
    {
        private readonly Timer reloj = new Timer();
        private readonly Stopwatch cronometro = new Stopwatch();
        private string texto = "";
        private float desplazamiento;
        private int anchoCiclo;
        private const int Separacion = 120;

        public float PixelesPorSegundo { get; set; }

        public string Texto
        {
            get { return texto; }
            set
            {
                string nuevo = value ?? "";
                if (nuevo == texto) return;
                texto = nuevo;
                desplazamiento = 0;
                anchoCiclo = 0;
                Invalidate();
            }
        }

        public LetreroLed()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            PixelesPorSegundo = 100F;
            BackColor = Color.Black;
            ForeColor = Color.Lime;
            Font = new Font("Consolas", 20F, FontStyle.Bold);
            reloj.Interval = 25;
            reloj.Tick += delegate { Avanzar(); };
            cronometro.Start();
            reloj.Start();
        }

        private void Avanzar()
        {
            float segundos = (float)cronometro.Elapsed.TotalSeconds;
            cronometro.Reset();
            cronometro.Start();
            if (segundos > 0.1F) segundos = 0.1F;
            if (!Visible || IsDisposed || String.IsNullOrEmpty(texto)) return;
            desplazamiento += PixelesPorSegundo * segundos;
            if (anchoCiclo > 0 && desplazamiento >= anchoCiclo) desplazamiento -= anchoCiclo * (int)(desplazamiento / anchoCiclo);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (String.IsNullOrEmpty(texto)) return;
            TextFormatFlags banderas = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
            Size medida = TextRenderer.MeasureText(g, texto, Font, new Size(Int32.MaxValue, Int32.MaxValue), banderas);
            anchoCiclo = Math.Max(1, medida.Width + Separacion);
            int y = Math.Max(0, (Height - medida.Height) / 2);
            for (float x = desplazamiento - anchoCiclo; x < Width; x += anchoCiclo)
                TextRenderer.DrawText(g, texto, Font, new Point((int)x, y), ForeColor, banderas);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { reloj.Stop(); reloj.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
