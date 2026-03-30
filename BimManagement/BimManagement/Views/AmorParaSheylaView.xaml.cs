using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace BimManagement
{
    public partial class AmorParaSheylaView : Window
    {
        private readonly DispatcherTimer _timer;
        private int _secondsLeft = 10;

        public AmorParaSheylaView(string mensaje)
        {
            InitializeComponent();
            MensajeText.Text = mensaje;
            UpdateCountdown();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _secondsLeft--;
            UpdateCountdown();
            if (_secondsLeft <= 0)
                Close();
        }

        private void UpdateCountdown()
        {
            CountdownText.Text = $"Cerrando en {_secondsLeft}s";
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            base.OnClosed(e);
        }

        private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
    }
}
