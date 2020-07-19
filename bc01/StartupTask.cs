using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using System.Diagnostics;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using Windows.Devices.Enumeration;
using System.Data.SqlClient;

// Add using statements to the GrovePi libraries
using GrovePi;
using GrovePi.Sensors;
using Windows.Media.Audio;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace bc01
{
    public sealed class StartupTask : IBackgroundTask
    {
        BackgroundTaskDeferral deferral;
        private ThreadPoolTimer timer;
        IBackgroundTaskInstance _taskInstance = null;

        BackgroundTaskCancellationReason _cancelReason = BackgroundTaskCancellationReason.Abort;
        volatile bool _cancelRequested = false;


        private SerialDevice serialPort = null;

        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;

        IDHTTemperatureAndHumiditySensor sensor = DeviceFactory.Build.DHTTemperatureAndHumiditySensor(Pin.DigitalPin4, DHTModel.Dht11);

        int brightness = 0;
        long pocitadlo = 0;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //

            Debug.WriteLine("Background " + taskInstance.Task.Name + " Starting...");
            deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += new BackgroundTaskCanceledEventHandler(OnCanceled);

            _taskInstance = taskInstance;

            await InitGPIOAsync();

            timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromSeconds(30));

        }


        string aqs = string.Empty;
        DeviceInformationCollection dis;

        private async System.Threading.Tasks.Task InitGPIOAsync()
        {
            aqs = SerialDevice.GetDeviceSelector();
            dis = await DeviceInformation.FindAllAsync(aqs);
            using (SerialDevice serialPort = await SerialDevice.FromIdAsync(dis[0].Id))
            {
                /* Configure serial settings */
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                serialPort.BaudRate = 9600;                                             /* mini UART: only standard baudrates */
                serialPort.Parity = SerialParity.None;                                  /* mini UART: no parities */
                serialPort.StopBits = SerialStopBitCount.One;                           /* mini UART: 1 stop bit */
                serialPort.DataBits = 8;

                /* Write a string out over serial */
                // turn on autocalibration
                byte[] send79Buffer = { 0XFF, 0x01, 0x79, 0xA0, 0x00, 0x00, 0x00, 0x00, 0x79 };
                DataWriter dataWriter = new DataWriter();
                //dataWriter.WriteString(txBuffer);

                dataWriter.WriteBytes(send79Buffer);

                uint bytesWritten = await serialPort.OutputStream.WriteAsync(dataWriter.DetachBuffer());

                Debug.WriteLine("Autokalibrace vypnuta.");


            }

        }


        private async void Timer_Tick(ThreadPoolTimer timer)
        {
            string sensortemp;
            string sensorhum;
            double dsensortemp = 0.0;
            double dsensorhum = 0.0;

            double dco2 = 0.0;

            if (_cancelRequested == false)
            {
                Debug.WriteLine($"------------------------------------------------------ Blick {pocitadlo++}!!!");
                try
                {
                    // Check the value of the Sensor.
                    // Temperature in Celsius is returned as a double type.  Convert it to string so we can print it.
                    sensor.Measure();
                    //sensortemp = sensor.TemperatureInCelsius.ToString();
                    dsensortemp = sensor.TemperatureInCelsius;
                    // Same for Humidity.  
                    //sensorhum = sensor.Humidity.ToString();
                    dsensorhum = sensor.Humidity;

                    // Print all of the values to the debug window.  
                    System.Diagnostics.Debug.WriteLine("Temp is " + dsensortemp + " C.  And the Humidity is " + dsensorhum + "%. ");

                }
                catch (Exception ex)
                {
                    // NOTE: There are frequent exceptions of the following:
                    // WinRT information: Unexpected number of bytes was transferred. Expected: '. Actual: '.
                    // This appears to be caused by the rapid frequency of writes to the GPIO
                    // These are being swallowed here/

                    // If you want to see the exceptions uncomment the following:
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                }

                try
                {

                    using ( SerialDevice  serialPort = await SerialDevice.FromIdAsync(dis[0].Id))
                    {
                        /* Configure serial settings */
                        serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                        serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                        serialPort.BaudRate = 9600;                                             /* mini UART: only standard baudrates */
                        serialPort.Parity = SerialParity.None;                                  /* mini UART: no parities */
                        serialPort.StopBits = SerialStopBitCount.One;                           /* mini UART: 1 stop bit */
                        serialPort.DataBits = 8;

                        /* Write a string out over serial */
                        //string txBuffer = "Hello Serial";
                        byte[] send86Buffer = { 0XFF, 0x01, 0x86, 0x00, 0x00, 0x00, 0x00, 0x00, 0x79 };
                        DataWriter dataWriter = new DataWriter();
                        //dataWriter.WriteString(txBuffer);

                        dataWriter.WriteBytes(send86Buffer);

                        uint bytesWritten = await serialPort.OutputStream.WriteAsync(dataWriter.DetachBuffer());

                        /* Read data in from the serial port */
                        const uint maxReadLength = 1024;
                        DataReader dataReader = new DataReader(serialPort.InputStream);
                        uint bytesToRead = await dataReader.LoadAsync(maxReadLength);
                        byte[] recv86Buffer = new byte[bytesToRead];
                        dataReader.ReadBytes(recv86Buffer);
                        //string koko = System.Convert.ToString(recv86Buffer[0],16)
                        dco2 = recv86Buffer[2] * 256 + recv86Buffer[3];
                        Debug.WriteLine("Přišlo: " +
                            System.Convert.ToString(recv86Buffer[0], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[1], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[2], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[3], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[4], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[5], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[6], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[7], 16) + ", " +
                            System.Convert.ToString(recv86Buffer[8], 16) +
                            "; Gas concentration = " + dco2.ToString() + " ;");


                    }

                    try
                    {
                        var cb = new SqlConnectionStringBuilder();
                        cb.DataSource = "iot01.database.windows.net";
                        cb.UserID = "ivanj9";
                        cb.Password = "Hovnajs44;";
                        cb.InitialCatalog = "iot01";

                        using (var connection = new SqlConnection(cb.ConnectionString))
                        {
                            connection.Open();


                            String queryx = "INSERT INTO dbo.data_co2 (datum, teplota, vlhkost, co2, senzorokno, senzorosoba) VALUES (@datum, @teplota, @vlhkost, @co2, @senzorokno, @senzorosoba)";

                            using (SqlCommand command = new SqlCommand(queryx, connection))
                            {
                                command.Parameters.AddWithValue("@datum", DateTime.Now);
                                command.Parameters.AddWithValue("@teplota", dsensortemp);
                                command.Parameters.AddWithValue("@vlhkost", dsensorhum);
                                command.Parameters.AddWithValue("@co2", dco2);
                                command.Parameters.AddWithValue("@senzorokno", 0);
                                command.Parameters.AddWithValue("@senzorosoba", 0);

                                //connection.Open();
                                int result = command.ExecuteNonQuery();

                                // Check Error
                                if (result < 0)
                                    Console.WriteLine("Error inserting data into Database!");
                            }

                        }
                    }
                    catch (SqlException e)
                    {
                        Console.WriteLine(e.ToString());
                    }




                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    //throw;
                }


            }
            else
            {
                Debug.WriteLine("Timer cancel!!!");

                timer.Cancel();
                //
                // Indicate that the background task has completed.
                //
                deferral.Complete();
            }

        }
        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            //
            // Indicate that the background task is canceled.
            //
            _cancelRequested = true;
            _cancelReason = reason;

            Debug.WriteLine("Background " + sender.Task.Name + " Cancel Requested...");
        }


    }
}
