using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using RockolaKaraoke.Servicios;

namespace RockolaKaraoke.Interfaz
{
    public class FormularioMenuTeclado : Form
    {
        protected readonly List<Button> opciones = new List<Button>();
        protected int indice;

        protected void RegistrarOpcion(Button boton)
        {
            opciones.Add(boton);
            boton.TabStop = false;
            boton.FlatStyle = FlatStyle.Flat;
            boton.FlatAppearance.BorderSize = 1;
            boton.FlatAppearance.BorderColor = Color.Gray;
            boton.Cursor = Cursors.Hand;
            // No cambiar la selección del teclado solo porque el puntero esté encima.
            // El mouse conserva el clic normal; las flechas mantienen su selección propia.
        }

        protected virtual void PintarOpcion(Button boton, bool seleccionada)
        {
            boton.BackColor = seleccionada ? Color.FromArgb(245, 190, 0) : Color.FromArgb(225, 225, 225);
            boton.ForeColor = Color.Black;
        }

        protected void ResaltarOpcion()
        {
            for (int i = 0; i < opciones.Count; i++) PintarOpcion(opciones[i], i == indice);
        }

        private void Mover(int paso)
        {
            if (opciones.Count == 0) return;
            indice = (indice + paso + opciones.Count) % opciones.Count;
            ResaltarOpcion();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys tecla = keyData & Keys.KeyCode;
            AccionTeclado? accion = ConfiguracionTeclado.Buscar(tecla);
            // Los menús usan las mismas asignaciones que el reproductor.
            // No forzar flechas, Enter o Escape si el usuario cambió esas teclas.
            bool arriba = accion.HasValue && accion.Value == AccionTeclado.Arriba;
            bool abajo = accion.HasValue && accion.Value == AccionTeclado.Abajo;
            // En menús de una sola columna, izquierda/derecha también recorren opciones.
            if (accion.HasValue && accion.Value == AccionTeclado.Izquierda) arriba = true;
            if (accion.HasValue && accion.Value == AccionTeclado.Derecha) abajo = true;
            bool aceptar = accion.HasValue && accion.Value == AccionTeclado.Agregar;
            bool cancelar = accion.HasValue && (accion.Value == AccionTeclado.Cancelar || accion.Value == AccionTeclado.Menu);
            if (arriba) { Mover(-1); return true; }
            if (abajo) { Mover(1); return true; }
            if (aceptar) { if (indice >= 0 && indice < opciones.Count) opciones[indice].PerformClick(); return true; }
            if (cancelar) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    public class FormularioMenuPrincipal : FormularioMenuTeclado
    {
        public string Seleccion = "";

        public FormularioMenuPrincipal()
        {
            Text = "MENÚ";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            BackColor = Color.FromArgb(215, 215, 215);

            string[,] datos = new string[,]
            {
                { "CONFIGURAR MUSICA", "MUSICA" },
                { "CONFIGURAR LETREROS LED", "LETREROS" },
                { "CONFIGURAR TECLADO", "TECLADO" },
                { "CERRAR", "CERRAR" },
                { "CERRAR PROGRAMA", "SALIR" },
                { "APAGAR PC", "APAGAR" },
                { "ACERCA DE...", "ACERCA" }
            };
            int alto = 50;
            ClientSize = new Size(560, 20 + datos.GetLength(0) * alto);
            for (int i = 0; i < datos.GetLength(0); i++)
            {
                string clave = datos[i, 1];
                Button b = new Button { Text = datos[i, 0], Left = 15, Top = 10 + i * alto, Width = 530, Height = alto - 6, Font = new Font("Impact", 20F) };
                RegistrarOpcion(b);
                b.Click += delegate { Seleccion = clave; Close(); };
                Controls.Add(b);
            }
            ResaltarOpcion();
        }
    }

    public class CapturaTecla : Form
    {
        public Keys Tecla = Keys.None;

        public CapturaTecla(string accion)
        {
            Text = "Asignar tecla";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(520, 170);
            BackColor = Color.FromArgb(5, 14, 32);
            ForeColor = Color.White;
            Label mensaje = new Label { Text = "Presiona la tecla nueva para:\n" + accion, Dock = DockStyle.Top, Height = 100, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 15F, FontStyle.Bold), ForeColor = Color.Gold };
            Button cancelar = new Button { Text = "CANCELAR (mouse)", Left = 160, Top = 112, Width = 200, Height = 40, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 105, 175), ForeColor = Color.White, TabStop = false };
            cancelar.Click += delegate { DialogResult = DialogResult.Cancel; };
            Controls.Add(mensaje); Controls.Add(cancelar);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys tecla = keyData & Keys.KeyCode;
            if (tecla == Keys.ControlKey || tecla == Keys.ShiftKey || tecla == Keys.Menu || tecla == Keys.None)
                return base.ProcessCmdKey(ref msg, keyData);
            Tecla = tecla;
            DialogResult = DialogResult.OK;
            return true;
        }
    }
}
