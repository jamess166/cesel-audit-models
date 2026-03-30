using BimManagement;
using System;
using System.Collections.Generic;

public static class AmorParaSheylaNew
{
    private static readonly List<string> MensajesDeAmor = new List<string>
    {
        "❤️ Te amo con todo mi corazón 💕",
        "🥰 Gracias a ti, soy el esposo más feliz del mundo.",
        "✨ Que tengas una semana grandiosa mi vida. Recuerda que Te Amo 💖",
        "🎬 Si te sale este mensaje, es una señal del universo que debemos ir al cine hoy 🍿",
        "🌹 Eres lo mejor que me pasó en la vida. Te amo, mi Sheyla.",
        "💌 Solo quería recordarte que pienso en ti aunque esté trabajando. ¡Te amo!",
        "🌙 Cada día que pasa me enamoro más de ti. Gracias por existir 💫",
    };

    public static void MostrarMensaje()
    {
        var random = new Random();

        int totalOpciones = MensajesDeAmor.Count + 1;
        int index = random.Next(totalOpciones);

        string mensaje;

        if (index == MensajesDeAmor.Count)
        {
            // Mensaje especial
            mensaje = GenerarMensajeEspecial();
        }
        else
        {
            // Mensajes normales
            mensaje = MensajesDeAmor[index];
        }

        var ventana = new AmorParaSheylaView(mensaje);
        ventana.ShowDialog();
    }

    public static string GenerarMensajeEspecial()
    {
        DateTime inicio = new DateTime(2025, 2, 1);
        string tiempo = ObtenerTiempoTranscurrido(inicio);

        return $"💖 Este mensaje es para recordarte que en todo este tiempo ({tiempo}) he sido muy feliz a tu lado.";
    }

    public static string ObtenerTiempoTranscurrido(DateTime fechaInicio)
    {
        DateTime hoy = DateTime.Today;

        int años = hoy.Year - fechaInicio.Year;
        int meses = hoy.Month - fechaInicio.Month;
        int dias = hoy.Day - fechaInicio.Day;

        if (dias < 0)
        {
            meses--;
            dias += DateTime.DaysInMonth(hoy.AddMonths(-1).Year, hoy.AddMonths(-1).Month);
        }

        if (meses < 0)
        {
            años--;
            meses += 12;
        }

        return FormatearTiempo(años, meses, dias);
    }

    private static string FormatearTiempo(int años, int meses, int dias)
    {
        List<string> partes = new List<string>();

        if (años > 0)
            partes.Add($"{años} {(años == 1 ? "año" : "años")}");

        if (meses > 0)
            partes.Add($"{meses} {(meses == 1 ? "mes" : "meses")}");

        if (dias > 0)
            partes.Add($"{dias} {(dias == 1 ? "día" : "días")}");

        return string.Join(", ", partes);
    }

    //public static void MostrarMensaje()
    //{
    //    var random = new Random();
    //    int index = random.Next(MensajesDeAmor.Count);
    //    string mensaje = MensajesDeAmor[index];

    //    var ventana = new AmorParaSheylaView(mensaje);
    //    ventana.ShowDialog();
    //}
}
