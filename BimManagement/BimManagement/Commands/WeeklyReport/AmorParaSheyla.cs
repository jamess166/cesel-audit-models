using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

public static class AmorParaSheyla
{
    private static readonly List<string> MensajesDeAmor = new List<string>
    {
        "Te amo con todo mi corazón",
        "Gracias a ti, soy el esposo mas feliz del mundo.",
        "Que tengas una semana grandiosa mi vida, Recuerda que Te Amo",
        "Si te sale este mensaje, es porque es una señal que debemos ir al cine hoy"
    };

    public static void MostrarMensaje(UIControlledApplication app = null)
    {
        var random = new Random();
        int index = random.Next(MensajesDeAmor.Count);
        string mensaje = MensajesDeAmor[index];

        TaskDialog.Show("❤️ Para Sheyla", mensaje);
    }
}
