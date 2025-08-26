using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;

public static class AmorParaSheyla
{
    private static readonly List<string> MensajesDeAmor = new List<string>
    {
        "Cada día contigo, Sheyla, es un regalo que nunca dejaré de agradecer.",
        "Sheyla, tu sonrisa ilumina mi mundo incluso en los días más grises.",
        "Eres mi compañera perfecta, mi amor eterno.",
        "Tu amor es el motor que me impulsa a ser mejor cada día.",
        "Estar contigo es mi lugar favorito en el mundo.",
        "Te amo más de lo que las palabras pueden expresar.",
        "Sheyla, contigo todo es más bonito.",
        "Desde que llegaste, mi corazón late con más alegría.",
        "Tu risa es mi melodía favorita.",
        "Sheyla, eres mi hogar, mi paz, mi todo.",
        "Contigo, incluso lo ordinario se vuelve extraordinario.",
        "Gracias por elegirme todos los días.",
        "No necesito suerte, te tengo a ti.",
        "Eres el sueño que nunca quiero dejar de vivir.",
        "Mi amor por ti crece más cada día.",
        "Sheyla, eres mi inspiración constante.",
        "Tú y yo, hoy y siempre.",
        "No hay nadie como tú, y eso me hace el hombre más afortunado.",
        "Tu amor me hace invencible.",
        "En tus ojos encuentro mi refugio.",
        "Gracias por ser tú, Sheyla.",
        "Solo tú haces latir mi corazón así.",
        "A tu lado aprendí el verdadero significado de amar.",
        "Sheyla, gracias por llenar mi vida de ternura.",
        "Cada vez que pienso en ti, sonrío.",
        "Nuestro amor es mi mejor historia.",
        "Amarte es lo más natural del mundo.",
        "Si tuviera que elegir de nuevo, siempre te elegiría a ti.",
        "Contigo, la vida tiene más sentido.",
        "Eres mi persona favorita.",
        "Sheyla, eres mi siempre."
    };

    public static void MostrarMensaje(UIControlledApplication app = null)
    {
        var random = new Random();
        int index = random.Next(MensajesDeAmor.Count);
        string mensaje = MensajesDeAmor[index];

        TaskDialog.Show("❤️ Para Sheyla", mensaje);
    }
}
