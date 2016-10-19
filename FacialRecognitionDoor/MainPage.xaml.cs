using IotPrototype.FacialRecognition;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using IotPrototype.Helpers;
using IotPrototype.Objects;
using Microsoft.ProjectOxford.Face;
using System.Text;
using System.Linq;
using Sensors.Dht;
using Windows.Devices.I2c;
using IotPrototype.Sensors;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml.Media;
using Windows.UI;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IotPrototype
{
    public sealed partial class MainPage : Page
    {
        private bool MoveEnabled = true;
        private bool TempEnabled = true;
        private bool LightEnabled = true;


        private const string I2C_CONTROLLER_NAME = "I2C1";

        // Webcam Related Variables:
        private WebcamHelper webcam;

        // Oxford Related Variables:
        private bool initializedOxford = false;

        // Whitelist Related Variables:
        private List<Visitor> whitelistedVisitors = new List<Visitor>();
        private StorageFolder whitelistFolder;
        private bool currentlyUpdatingWhitelist;

        // Speech Related Variables:
        private SpeechHelper speech;

        // GPIO Related Variables:
        private GpioHelper gpioHelper;
        private bool gpioAvailable;
        private bool doorbellJustPressed = false;

        // GUI Related Variables:
        private double visitorIDPhotoGridMaxWidth = 0;


        private DispatcherTimer _timer = null;

        private GpioPin _temperaturePin = null;

        private DateTime _lastUpdatedTemp = DateTime.Now;

        private IDht _dht = null;

        //Light sensor

        // I2C Device
        private I2cDevice I2CDev;        
        // TSL Sensor
        private TSL2561 TSL2561Sensor;

        // TSL Gain and MS Values
        private Boolean Gain = false;
        private uint MS = 0;

        /// <summary>
        /// Called when the page is first navigated to.
        /// </summary>
        public MainPage()
        {
            InitializeComponent();

            // Causes this page to save its state when navigating to other pages
            NavigationCacheMode = NavigationCacheMode.Enabled;



            if (initializedOxford == false)
            {
                // If Oxford facial recognition has not been initialized, attempt to initialize it
                InitializeOxford();
            }

            if (gpioAvailable == false)
            {
                // If GPIO is not available, attempt to initialize it
                //InitializeGpio();
            }

            // If user has set the DisableLiveCameraFeed within Constants.cs to true, disable the feed:
            if (GeneralConstants.DisableLiveCameraFeed)
            {
                LiveFeedPanel.Visibility = Visibility.Collapsed;
                DisabledFeedGrid.Visibility = Visibility.Visible;
            }
            else
            {
                LiveFeedPanel.Visibility = Visibility.Visible;
                DisabledFeedGrid.Visibility = Visibility.Collapsed;
            }

            // Initialize I2C Device
            if (LightEnabled)
            {
                InitializeI2CDevice();
            }

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(5);
            _timer.Tick += _timer_Tick;


            

        }

        private async void InitializeI2CDevice()
        {
            try
            {
                // Initialize I2C device
                var settings = new I2cConnectionSettings(TSL2561.TSL2561_ADDR);

                settings.BusSpeed = I2cBusSpeed.FastMode;
                settings.SharingMode = I2cSharingMode.Shared;

                string aqs = I2cDevice.GetDeviceSelector(I2C_CONTROLLER_NAME);  /* Find the selector string for the I2C bus controller                   */
                var dis = await DeviceInformation.FindAllAsync(aqs);            /* Find the I2C bus controller device with our selector string           */

                I2CDev = await I2cDevice.FromIdAsync(dis[0].Id, settings);    /* Create an I2cDevice with our selected bus controller and I2C settings */
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());

                return;
            }

            initializeSensor();
        }

        private void initializeSensor()
        {
            // Initialize Sensor
            TSL2561Sensor = new TSL2561(ref I2CDev);

            // Set the TSL Timing
            MS = (uint)TSL2561Sensor.SetTiming(false, 2);
            // Powerup the TSL sensor
            TSL2561Sensor.PowerUp();

            Debug.WriteLine("TSL2561 ID: " + TSL2561Sensor.GetId());
        }


        private async void _timer_Tick(object sender, object e)
        {

            if (TempEnabled)
            {
                DhtReading reading = new DhtReading();
                reading = await _dht.GetReadingAsync().AsTask();

                if (reading.IsValid)
                {
                    //this.TotalSuccess++;
                    this.TemperatureDisplay.Text = string.Format("{0:0.0} °C", Convert.ToSingle(reading.Temperature));
                    this.HumidityDisplay.Text = string.Format("{0:0.0}% RH", Convert.ToSingle(reading.Humidity));
                    //this.LastUpdatedDisplay = DateTimeOffset.Now;                
                }


                Debug.WriteLine(string.Format("{0:0.0}% RH", Convert.ToSingle(reading.Humidity)));
                Debug.WriteLine(string.Format("{0:0.0} °C", Convert.ToSingle(reading.Temperature)));
            }



            //this.OnPropertyChanged(nameof(LastUpdatedDisplay));


            // Retrive luminosity and update the screen
            if (LightEnabled)
            {
                uint[] Data = TSL2561Sensor.GetData();

                Debug.WriteLine("Data1: " + Data[0] + ", Data2: " + Data[1]);

                double luxValue = TSL2561Sensor.GetLux(Gain, MS, Data[0], Data[1]);
                string info = String.Format("{0:0.0} lux", luxValue);
                LightDisplay.Text = info;

                Debug.WriteLine("Light: " + info);

                if (luxValue <= 250 || luxValue >= 750)
                {
                    LightDisplay.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0));
                }
                else if (luxValue <= 380 || luxValue >= 620)
                {
                    LightDisplay.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 204, 0));
                }
                else
                {
                    LightDisplay.Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 255, 0));
                }
            }
        }

        /// <summary>
        /// Triggered every time the page is navigated to.
        /// </summary>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (initializedOxford)
            {
                UpdateWhitelistedVisitors();
            }

            try
            {
                //, 

                if (TempEnabled)
                {
                    _temperaturePin = GpioController.GetDefault().OpenPin(4, GpioSharingMode.Exclusive);
                    _dht = new Dht22(_temperaturePin, GpioPinDriveMode.Input);
                }

                if (MoveEnabled)
                {
                    InitalizeMoveDetection();
                }

                if (TempEnabled || LightEnabled)
                {
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }


        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            _timer.Stop();

            _temperaturePin.Dispose();
            _temperaturePin = null;

            _dht = null;

            //movePin.ValueChanged -= MovePin_ValueChanged;


            base.OnNavigatedFrom(e);
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes Oxford facial recognition.
        /// </summary>
        public async void InitializeOxford()
        {
            // initializedOxford bool will be set to true when Oxford has finished initialization successfully
            initializedOxford = await OxfordFaceAPIHelper.InitializeOxford();

            // Populates UI grid with whitelisted visitors
            UpdateWhitelistedVisitors();
        }

        /// <summary>
        /// Called once, when the app is first opened. Initializes device GPIO.
        /// </summary>
        public void InitializeGpio()
        {
            try
            {
                // Attempts to initialize application GPIO.
                gpioHelper = new GpioHelper();
                gpioAvailable = gpioHelper.Initialize();
            }
            catch
            {
                // This can fail if application is run on a device, such as a laptop, that does not have a GPIO controller
                gpioAvailable = false;
                Debug.WriteLine("GPIO controller not available.");
            }

            // If initialization was successfull, attach doorbell pressed event handler
            if (gpioAvailable)
            {
                gpioHelper.GetDoorBellPin().ValueChanged += DoorBellPressed;
            }
        }

        /// <summary>
        /// Triggered when webcam feed loads both for the first time and every time page is navigated to.
        /// If no WebcamHelper has been created, it creates one. Otherwise, simply restarts webcam preview feed on page.
        /// </summary>
        private async void WebcamFeed_Loaded(object sender, RoutedEventArgs e)
        {
            if (webcam == null || !webcam.IsInitialized())
            {
                // Initialize Webcam Helper
                webcam = new WebcamHelper();
                await webcam.InitializeCameraAsync();

                // Set source of WebcamFeed on MainPage.xaml
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    // Start the live feed
                    await webcam.StartCameraPreview();
                }
            }
            else if (webcam.IsInitialized())
            {
                WebcamFeed.Source = webcam.mediaCapture;

                // Check to make sure MediaCapture isn't null before attempting to start preview. Will be null if no camera is attached.
                if (WebcamFeed.Source != null)
                {
                    await webcam.StartCameraPreview();
                }
            }
        }

        /// <summary>
        /// Triggered when media element used to play synthesized speech messages is loaded.
        /// Initializes SpeechHelper and greets user.
        /// </summary>
        private async void speechMediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (speech == null)
            {
                speech = new SpeechHelper(speechMediaElement);
                //await speech.Read(SpeechContants.InitialGreetingMessage);
            }
            else
            {
                // Prevents media element from re-greeting visitor
                speechMediaElement.AutoPlay = false;
            }
        }

        /// <summary>
        /// Triggered when the whitelisted users grid is loaded. Sets the size of each photo within the grid.
        /// </summary>
        private void WhitelistedUsersGrid_Loaded(object sender, RoutedEventArgs e)
        {
            visitorIDPhotoGridMaxWidth = (WhitelistedUsersGrid.ActualWidth / 3) - 10;
        }

        /// <summary>
        /// Triggered when user presses physical door bell button
        /// </summary>
        private async void DoorBellPressed(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (!doorbellJustPressed)
            {
                // Checks to see if even was triggered from a press or release of button
                if (args.Edge == GpioPinEdge.FallingEdge)
                {
                    //Doorbell was just pressed
                    doorbellJustPressed = true;

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        await DoorbellPressed();
                    });

                }
            }
        }

        /// <summary>
        /// Triggered when user presses virtual doorbell app bar button
        /// </summary>
        private async void DoorbellButton_Click(object sender, RoutedEventArgs e)
        {
            if (!doorbellJustPressed)
            {
                doorbellJustPressed = true;
                await DoorbellPressed();
            }
        }

        /// <summary>
        /// Called when user hits physical or vitual doorbell buttons. Captures photo of current webcam view and sends it to Oxford for facial recognition processing.
        /// </summary>
        private async Task DoorbellPressed()
        {
            // Display analysing visitors grid to inform user that doorbell press was registered
            AnalysingVisitorGrid.Visibility = Visibility.Visible;

            // List to store visitors recognized by Oxford Face API
            // Count will be greater than 0 if there is an authorized visitor at the door
            List<string> recognizedVisitors = new List<string>();

            // Confirms that webcam has been properly initialized and oxford is ready to go
            if (webcam.IsInitialized() && initializedOxford)
            {
                // Stores current frame from webcam feed in a temporary folder
                StorageFile image = await webcam.CapturePhoto();

                //try
                //{
                //    // Oxford determines whether or not the visitor is on the Whitelist and returns true if so
                //    recognizedVisitors = await OxfordFaceAPIHelper.IsFaceInWhitelist(image);
                //}
                //catch (FaceRecognitionException fe)
                //{
                //    switch (fe.ExceptionType)
                //    {
                //        // Fails and catches as a FaceRecognitionException if no face is detected in the image
                //        case FaceRecognitionExceptionType.NoFaceDetected:
                //            Debug.WriteLine("WARNING: No face detected in this image.");
                //            break;
                //    }
                //}
                //catch (FaceAPIException faceAPIEx)
                //{
                //    Debug.WriteLine("FaceAPIException in IsFaceInWhitelist(): " + faceAPIEx.ErrorMessage);
                //}
                //catch
                //{
                //    // General error. This can happen if there are no visitors authorized in the whitelist
                //    Debug.WriteLine("WARNING: Oxford just threw a general expception.");
                //}

                //if(recognizedVisitors.Count > 0)
                //{
                //    // If everything went well and a visitor was recognized, unlock the door:
                //    UnlockDoor(recognizedVisitors[0]);
                //}
                //else
                //{
                //    // Otherwise, inform user that they were not recognized by the system
                //    await speech.Read(SpeechContants.VisitorNotRecognizedMessage);
                //}

                try
                {
                    var result = await EmotionHelper.DetectEmotions(image);
                    var emotions = result.Item1;
                    var faceAttributes = result.Item2;
                    var allEmotions = emotions.Scores.ToRankedList().Where(i => i.Value > 0.1).OrderByDescending(i => i.Value);

                    var builder = new StringBuilder();
                    if (faceAttributes.Glasses != Microsoft.ProjectOxford.Face.Contract.Glasses.NoGlasses)
                    {
                        builder.Append("You have glasses.");
                    }

                    builder.Append($"You are {(int)faceAttributes.Age} old { faceAttributes.Gender}. ");

                    builder.Append("You feel ");
                    foreach (var emotion in allEmotions)
                    {
                        builder.Append($"{(int)(emotion.Value * 100)} percent {emotion.Key} ");
                    }

                    await speech.Read(builder.ToString());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }


            }
            else
            {
                if (!webcam.IsInitialized())
                {
                    // The webcam has not been fully initialized for whatever reason:
                    Debug.WriteLine("Unable to analyze visitor at door as the camera failed to initlialize properly.");
                    await speech.Read(SpeechContants.NoCameraMessage);
                }

                if (!initializedOxford)
                {
                    // Oxford is still initializing:
                    Debug.WriteLine("Unable to analyze visitor at door as Oxford Facial Recogntion is still initializing.");
                }
            }

            doorbellJustPressed = false;
            AnalysingVisitorGrid.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Unlocks door and greets visitor
        /// </summary>
        private async void UnlockDoor(string visitorName)
        {
            // Greet visitor
            await speech.Read(SpeechContants.GeneralGreetigMessage(visitorName));

            if (gpioAvailable)
            {
                // Unlock door for specified ammount of time
                gpioHelper.UnlockDoor();
            }
        }

        /// <summary>
        /// Called when user hits vitual add user button. Navigates to NewUserPage page.
        /// </summary>
        private async void NewUserButton_Click(object sender, RoutedEventArgs e)
        {
            // Stops camera preview on this page, so that it can be started on NewUserPage
            await webcam.StopCameraPreview();

            //Navigates to NewUserPage, passing through initialized WebcamHelper object
            Frame.Navigate(typeof(NewUserPage), webcam);
        }

        /// <summary>
        /// Updates internal list of of whitelisted visitors (whitelistedVisitors) and the visible UI grid
        /// </summary>
        private async void UpdateWhitelistedVisitors()
        {
            // If the whitelist isn't already being updated, update the whitelist
            if (!currentlyUpdatingWhitelist)
            {
                currentlyUpdatingWhitelist = true;
                await UpdateWhitelistedVisitorsList();
                UpdateWhitelistedVisitorsGrid();
                currentlyUpdatingWhitelist = false;
            }
        }

        /// <summary>
        /// Updates the list of Visitor objects with all whitelisted visitors stored on disk
        /// </summary>
        private async Task UpdateWhitelistedVisitorsList()
        {
            // Clears whitelist
            whitelistedVisitors.Clear();

            // If the whitelistFolder has not been opened, open it
            if (whitelistFolder == null)
            {
                whitelistFolder = await KnownFolders.PicturesLibrary.CreateFolderAsync(GeneralConstants.WhiteListFolderName, CreationCollisionOption.OpenIfExists);
            }

            // Populates subFolders list with all sub folders within the whitelist folders.
            // Each of these sub folders represents the Id photos for a single visitor.
            var subFolders = await whitelistFolder.GetFoldersAsync();

            // Iterate all subfolders in whitelist
            foreach (StorageFolder folder in subFolders)
            {
                string visitorName = folder.Name;
                var filesInFolder = await folder.GetFilesAsync();

                var photoStream = await filesInFolder[0].OpenAsync(FileAccessMode.Read);
                BitmapImage visitorImage = new BitmapImage();
                await visitorImage.SetSourceAsync(photoStream);

                Visitor whitelistedVisitor = new Visitor(visitorName, folder, visitorImage, visitorIDPhotoGridMaxWidth);

                whitelistedVisitors.Add(whitelistedVisitor);
            }
        }

        /// <summary>
        /// Updates UserInterface list of whitelisted users from the list of Visitor objects (WhitelistedVisitors)
        /// </summary>
        private void UpdateWhitelistedVisitorsGrid()
        {
            // Reset source to empty list
            WhitelistedUsersGrid.ItemsSource = new List<Visitor>();
            // Set source of WhitelistedUsersGrid to the whitelistedVisitors list
            WhitelistedUsersGrid.ItemsSource = whitelistedVisitors;

            // Hide Oxford loading ring
            OxfordLoadingRing.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Triggered when the user selects a visitor in the WhitelistedUsersGrid
        /// </summary>
        private void WhitelistedUsersGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to UserProfilePage, passing through the selected Visitor object and the initialized WebcamHelper as a parameter
            Frame.Navigate(typeof(UserProfilePage), new UserProfileObject(e.ClickedItem as Visitor, webcam));
        }

        /// <summary>
        /// Triggered when the user selects the Shutdown button in the app bar. Closes app.
        /// </summary>
        private void ShutdownButton_Click(object sender, RoutedEventArgs e)
        {
            // Exit app
            Application.Current.Exit();
        }



        private void InitalizeMoveDetection()
        {
            var controller = GpioController.GetDefault();

            movePin = controller.OpenPin(17);

            movePin.DebounceTimeout = TimeSpan.FromMilliseconds(25);

            if (movePin.IsDriveModeSupported(GpioPinDriveMode.InputPullUp))
            {
                // Take advantage of built in pull-up resistors of Raspberry Pi 2 and DragonBoard 410c
                movePin.SetDriveMode(GpioPinDriveMode.InputPullUp);
            }
            else
            {
                // MBM does not support PullUp as it does not have built in pull-up resistors 
                movePin.SetDriveMode(GpioPinDriveMode.Input);
            }

            movePin.ValueChanged += MovePin_ValueChanged;

            /* DispatcherTimer timer = new DispatcherTimer();
             timer.Interval = new TimeSpan(0, 0, 0, 0, 500);
             timer.Tick += Timer_Tick;
             timer.Start();*/
        }

        private async void MovePin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            var gpioValue = sender.Read();

            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                MoveElypse.Visibility = (gpioValue == GpioPinValue.High ? Visibility.Collapsed : Visibility.Visible);
            });

            lastMoveValue = gpioValue;
        }

        private GpioPin movePin;
        private GpioPinValue lastMoveValue = GpioPinValue.High;

        private Random random = new Random();
        int currentTextIndex = 0;
        string[] bustedWords = new string[]
        {
            "Hello beauty",
            "Move bitch",
            "Busted",
            "Stop right there",
            "British homosexual",
            "Just Gay",
            "Detected",
            "Developper detected",
            "Casting couch",
            "Hello, Gorgeous!"
        };



        async private void Timer_Tick(object sender, object e)
        {
            return;
            var gpioValue = movePin.Read();
            MoveDetected.Visibility = gpioValue == GpioPinValue.High ? Visibility.Collapsed : Visibility.Visible;

            //  "Move bitch"
            if (lastMoveValue != gpioValue && gpioValue == GpioPinValue.Low)
            {
                string text = bustedWords[random.Next(9)];
                MoveDetected.Text = text;
                //currentTextIndex++;
                await speech.Read(text);
            }

            lastMoveValue = gpioValue;

            //int value = gpioValue == GpioPinValue.High ? 1 : 0;

            /*moveValues.Add(new KeyValuePair<DateTime, int>(DateTime.Now, value));*/


            //Debug.WriteLine("Value: " + value.ToString());

        }



    }
}
