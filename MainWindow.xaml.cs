using System;
using System.Globalization;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace PrometeoTicketHP {
 public partial class MainWindow : Window {
  public MainWindow() {
   InitializeComponent();
   Puertos.Items.Add("Sin seleccionar");
   try {
    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM")) {
     if (key != null) foreach (string name in key.GetValueNames()) {
      string port = key.GetValue(name) as string;
      if (!string.IsNullOrWhiteSpace(port) && !Puertos.Items.Contains(port)) Puertos.Items.Add(port);
     }
    }
   } catch (Exception) { /* La ventana funciona aunque no haya puertosDOEM. */ }
   Puertos.SelectedIndex = 0;
  }

  private bool IntentarMonto(out decimal monto) {
   if (!decimal.TryParse(Monto.Text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
       CultureInfo.InvariantCulture, out monto) || monto < 0 || monto > 999999.99m) {
    Estado.Text = "Ingresa un monto válido entre 0 y 999999.99.";
    return false;
   }
   return true;
  }

  // Regla solicitada: cada crédito equivale a cuatro pesos.
  private const decimal PesosPorCredito = 4.00m;
  private static decimal Creditos(decimal monto) => monto / PesosPorCredito;
  private static string Numero(decimal n) => n.ToString("0.##", CultureInfo.InvariantCulture);

  private void Monto_TextChanged(object sender, TextChangedEventArgs e) {
   if (Estado == null || CreditosVista == null || Monto == null) return;
   if (decimal.TryParse(Monto.Text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
       CultureInfo.InvariantCulture, out decimal monto) && monto >= 0) {
    CreditosVista.Text = "Créditos: " + Numero(Creditos(monto));
   } else CreditosVista.Text = "Ingresa un monto válido";
  }

  private void Preparar_Click(object sender, RoutedEventArgs e) {
   if (!IntentarMonto(out decimal monto)) return;
   Estado.Text = "Monto registrado: $" + monto.ToString("F2", CultureInfo.InvariantCulture) + " | Créditos: " + Numero(Creditos(monto)) + ".";
  }

  // Comunicación serial de la impresora: 2400 baudios, 8N1.
  private const int BaudiosImpresora = 2400;

  private void Imprimir_Click(object sender, RoutedEventArgs e) {
   if (!IntentarMonto(out decimal monto)) return;
   try {
    var documento = new FlowDocument {
     FontFamily = new FontFamily("Consolas"), FontSize = 13,
     PagePadding = new Thickness(20), ColumnWidth = double.PositiveInfinity
    };
    // Primer ticket: formato WILD JOKERS basado en el primer boleto del Datos_asc.DOEM
    string[] lineas = {
     "DATE: " + DateTime.Now.ToString("MM/dd/yy"),
     "TICKET#00000004 CREDIT VALUE = $" + PesosPorCredito.ToString("F2", CultureInfo.InvariantCulture),
     "** VOUCHER TICKET **",
     "**  WILD JOKERS  **",
     "----------------------------------------",
     "<<<   $ " + monto.ToString("F2", CultureInfo.InvariantCulture) + "  >>>",
     "CREDITS: " + Numero(Creditos(monto)),
     "----------------------------------------",
     "VOID IF MUTILATED         VAL# 036F68F6",
     "COPYRIGHT 1986-01 LEISURE TIME TECH.INC.",
     "----------------------------------------",
     "",
     "",
     "----------------------------------------",
     "LEISURE TIME TECH. VER:POG_595D/R595WBS1",
     "MACH#000001 BANK#001",
     "----------------------------------------",
     "<LEISURE TIME TECH.>",
     "******* VALID ON THIS DATE ONLY! *******",
     "TERMINAL#D249 BR#03 MK#0301 LICENSE#0000",
     "----------------------------------------"
    };
     // Si se selecciona un puerto COM, se envían las líneasDOEM
    // del ticket mediante comunicación serial a 2400 baudiosDOEM.
   // El texto se codifica en ASCII y se separa con CR/LFDOEM.
    string puertoSeleccionado = Puertos.SelectedItem as string;
    if (!string.IsNullOrWhiteSpace(puertoSeleccionado) && puertoSeleccionado != "Sin seleccionar") {
     using (var serial = new SerialPort(puertoSeleccionado, BaudiosImpresora,
         Parity.None, 8, StopBits.One)) {
      serial.Handshake = Handshake.None;
      serial.Encoding = Encoding.ASCII;
      serial.WriteTimeout = 5000;
      serial.Open();
      serial.Write(string.Join("\r\n", lineas) + "\r\n");
     }
     Estado.Text = "Ticket enviado a " + puertoSeleccionado + ".";
     return;
    }
    // Si no se elige COM, se conserva la impresión normal de WindowsDOEM.
    var dialogo = new PrintDialog();
    if (dialogo.ShowDialog() != true) {
     Estado.Text = "Impresión cancelada.";
     return;
    }
    foreach (string linea in lineas)
     documento.Blocks.Add(new Paragraph(new Run(linea)) { Margin = new Thickness(0), FontFamily = new FontFamily("Consolas"), FontSize = 11 });
    documento.PageWidth = dialogo.PrintableAreaWidth;
    documento.PageHeight = dialogo.PrintableAreaHeight;
    dialogo.PrintDocument(((IDocumentPaginatorSource)documento).DocumentPaginator, "Ticket HP - Prometeo");
    Estado.Text = "Ticket enviado a la impresora seleccionada.";
   } catch (Exception ex) {
    Estado.Text = "No se pudo imprimir: " + ex.Message;
   }
  }
 }
}
