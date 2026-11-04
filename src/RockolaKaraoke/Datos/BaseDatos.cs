using System;
using System.Data.SQLite;
using System.IO;
namespace RockolaKaraoke.Datos
{
 public static class BaseDatos
 {
  private static readonly string CarpetaDatos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RockolaKaraokeDaniel");
  public static string RutaArchivo { get { return Path.Combine(CarpetaDatos, "rockola.db"); } }
  private static string CadenaConexion { get { return "Data Source=" + RutaArchivo + ";Version=3;"; } }
  public static SQLiteConnection CrearConexion() { return new SQLiteConnection(CadenaConexion); }
  public static void Inicializar()
  {
   Directory.CreateDirectory(CarpetaDatos);
   if (!File.Exists(RutaArchivo)) SQLiteConnection.CreateFile(RutaArchivo);
   using (SQLiteConnection conexion = CrearConexion())
   {
    conexion.Open(); using (SQLiteCommand comando = conexion.CreateCommand())
    {
     comando.CommandText = @"
PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Generos (Id INTEGER PRIMARY KEY AUTOINCREMENT, Nombre TEXT NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE IF NOT EXISTS Artistas (Id INTEGER PRIMARY KEY AUTOINCREMENT, Nombre TEXT NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE IF NOT EXISTS Canciones (Id INTEGER PRIMARY KEY AUTOINCREMENT, GeneroId INTEGER NULL, ArtistaId INTEGER NULL, Titulo TEXT NOT NULL, RutaArchivo TEXT NOT NULL UNIQUE, Tipo TEXT NOT NULL DEFAULT 'Musica', DuracionSegundos INTEGER NOT NULL DEFAULT 0, Activa INTEGER NOT NULL DEFAULT 1, FOREIGN KEY(GeneroId) REFERENCES Generos(Id), FOREIGN KEY(ArtistaId) REFERENCES Artistas(Id));
CREATE TABLE IF NOT EXISTS ColaReproduccion (Id INTEGER PRIMARY KEY AUTOINCREMENT, CancionId INTEGER NOT NULL, FechaAgregada TEXT NOT NULL, Estado TEXT NOT NULL DEFAULT 'Pendiente', FOREIGN KEY(CancionId) REFERENCES Canciones(Id));
CREATE TABLE IF NOT EXISTS Configuracion (Clave TEXT PRIMARY KEY, Valor TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS MovimientosCreditos (Id INTEGER PRIMARY KEY AUTOINCREMENT, Tipo TEXT NOT NULL, Cantidad REAL NOT NULL, Fecha TEXT NOT NULL, Detalle TEXT NULL);
CREATE TABLE IF NOT EXISTS Publicidad (Id INTEGER PRIMARY KEY AUTOINCREMENT, RutaArchivo TEXT NOT NULL, Activa INTEGER NOT NULL DEFAULT 1, Orden INTEGER NOT NULL DEFAULT 0);";
     comando.ExecuteNonQuery();
    }
   }
  }
 }
}
