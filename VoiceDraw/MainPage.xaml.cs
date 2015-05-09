using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Media.SpeechSynthesis;
using Windows.Media.SpeechRecognition;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.ApplicationModel;
using Windows.Media.Capture;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Windows.UI.Core;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace VoiceDraw
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        double x = 10, y = 10;
        double angle = 0;

        void SetTurtle()
        {
            Canvas.SetLeft(Turtle, x);
            Canvas.SetTop(Turtle, y);
        }

        async Task Exec(string cmd, double param)
        {
            var px = x; var py = y;
            switch(cmd)
            {
                case "forw":
                    x += param * Math.Sin(Conv(angle));
                    y += param * Math.Cos(Conv(angle));
                    await Speak("Going forward");
                    break;
                case "back":
                    x -= param * Math.Sin(Conv(angle));
                    y -= param * Math.Cos(Conv(angle));
                    await Speak("Going Back");
                    break;
                case "left":
                    angle += param;
                    await Speak("Turning left");
                    break;
                case "right":
                    angle -= param;
                    await Speak("Turning right");
                    break;
            }
            if (px!=x || py!=y)
            {
                var P = new Line();
                P.X1 = px; P.Y1 = py;
                P.X2 = x; P.Y2 = y;
                P.Stroke = new SolidColorBrush(Colors.Black);
                P.StrokeThickness = 1;
                main.Children.Add(P);
            }
            SetTurtle();
        }

        private double Conv(double angle)
        {
            return angle / 180.0 * Math.PI;
        }

        public MainPage()
        {
            this.InitializeComponent();
            x = CoreWindow.GetForCurrentThread().Bounds.Width / 2;
            y = CoreWindow.GetForCurrentThread().Bounds.Height / 2;
            SetTurtle();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await InitializeRecognizer();
        }

        SpeechRecognizer speechRecognizer;
        SpeechSynthesizer synth = new SpeechSynthesizer();

        private async Task InitializeRecognizer()
        {
            bool permissionGained = await RequestMicrophonePermission();
            if (!permissionGained)
            {
                stat.Text = "No mic permission";
                return;
            }
            // Create an instance of SpeechRecognizer.
            speechRecognizer = new SpeechRecognizer();
            StorageFile grammarContentFile = await Package.Current.InstalledLocation.GetFileAsync(@"grammar.xml");
            SpeechRecognitionGrammarFileConstraint grammarConstraint = new SpeechRecognitionGrammarFileConstraint(grammarContentFile);
            speechRecognizer.Constraints.Add(grammarConstraint);
            SpeechRecognitionCompilationResult compilationResult = await speechRecognizer.CompileConstraintsAsync();

            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                stat.Text = "Error:" + compilationResult.Status.ToString();
                return;
            }

            // Set EndSilenceTimeout to give users more time to complete speaking a phrase.
            speechRecognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(1.2);
            speechRecognizer.StateChanged += SpeechRecognizer_StateChanged;
            speechRecognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            await speechRecognizer.ContinuousRecognitionSession.StartAsync();
        }

        private async Task Speak(string s)
        {
            var x = await synth.SynthesizeTextToStreamAsync(s);
            media.AutoPlay = true;
            media.SetSource(x, x.ContentType);
            media.Play();
        }

        private async void SpeechRecognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                () =>
                {
                    stat.Text = args.State.ToString();
                });
        }

        private async void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                async () =>
                {
                    var cmd = args.Result.SemanticInterpretation.Properties["cmd"][0].ToString();
                    var param = "";
                    if (args.Result.SemanticInterpretation.Properties.ContainsKey("param"))
                    {
                        param = args.Result.SemanticInterpretation.Properties["param"][0].ToString();
                    }
                    if (param=="")
                    {
                        if (cmd == "forw" || cmd == "back") param = "50";
                        if (cmd == "left" || cmd == "right") param = "90";
                    }
                    stat.Text = cmd+" "+param;
                    await Exec(cmd, double.Parse(param));
                    // "Recognized, conf="+args.Result.Confidence.ToString();
                });
        }

        private static int NoCaptureDevicesHResult = -1072845856;

        /// <summary>
        /// On desktop/tablet systems, users are prompted to give permission to use capture devices on a 
        /// per-app basis. Along with declaring the microphone DeviceCapability in the package manifest,
        /// this method tests the privacy setting for microphone access for this application.
        /// Note that this only checks the Settings->Privacy->Microphone setting, it does not handle
        /// the Cortana/Dictation privacy check, however (Under Settings->Privacy->Speech, Inking and Typing).
        /// 
        /// Developers should ideally perform a check like this every time their app gains focus, in order to 
        /// check if the user has changed the setting while the app was suspended or not in focus.
        /// </summary>
        /// <returns>true if the microphone can be accessed without any permissions problems.</returns>
        public async static Task<bool> RequestMicrophonePermission()
        {
            try
            {
                // Request access to the microphone only, to limit the number of capabilities we need
                // to request in the package manifest.
                MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings();
                settings.StreamingCaptureMode = StreamingCaptureMode.Audio;
                settings.MediaCategory = MediaCategory.Speech;
                MediaCapture capture = new MediaCapture();

                await capture.InitializeAsync(settings);
            }
            catch (UnauthorizedAccessException)
            {
                // The user has turned off access to the microphone. If this occurs, we should show an error, or disable
                // functionality within the app to ensure that further exceptions aren't generated when 
                // recognition is attempted.
                return false;
            }
            catch (Exception exception)
            {
                // This can be replicated by using remote desktop to a system, but not redirecting the microphone input.
                // Can also occur if using the virtual machine console tool to access a VM instead of using remote desktop.
                if (exception.HResult == NoCaptureDevicesHResult)
                {
                    var messageDialog = new Windows.UI.Popups.MessageDialog("No Audio Capture devices are present on this system.");
                    await messageDialog.ShowAsync();
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }


    }
}
