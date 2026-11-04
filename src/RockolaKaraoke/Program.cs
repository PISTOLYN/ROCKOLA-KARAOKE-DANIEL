using System;
using System.Windows.Forms;
using RockolaKaraoke.Datos;
using RockolaKaraoke.Interfaz;
namespace RockolaKaraoke
{
 internal static class Program
 {
  [STAThread] private static void Main()
  {
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   try { BaseDatos.Inicializar(); Application.Run(new FormularioPrincipal()); }
   catch (Exception ex) { MessageBox.Show("No se pudo iniciar la Rockola.\n\n" + ex.Message, "Error de inicio", MessageBoxButtons.OK, MessageBoxIcon.Error); }
  }
 }
}
