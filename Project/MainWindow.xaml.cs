using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using System.IO.IsolatedStorage;
using System.IO;

namespace Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //common list
        public static ObservableCollection<TempData> data = new ObservableCollection<TempData>();

        //stream reader
        static System.IO.StreamReader file = new System.IO.StreamReader("data.txt");
        //lock object
        private static object syncObj = new object();
        //static field counts the number of lines read in by each thread
        [ThreadStatic]
        private static int lineReadInCount = 0;

        //thread state
        static bool threadBusy = true;

        //background worker // thread 4
        BackgroundWorker bkw = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();
            //the event handlers
            bkw.DoWork += bkw_DoWork;
            bkw.ProgressChanged += bkw_ProgressChanged;
            bkw.RunWorkerCompleted += bkw_RunWorkerCompleted;

            //the properties 
            bkw.WorkerReportsProgress = true;
            bkw.WorkerSupportsCancellation = true;

            //combo-box
            ComboSeasons.Items.Add("Spring");
            ComboSeasons.Items.Add("Summer");
            ComboSeasons.Items.Add("Autumn");
            ComboSeasons.Items.Add("Winter");

        }
        public void ReadTempData()
        {
            string line;
            //enter lock
            Monitor.Enter(syncObj);
            try
            {
                //check line is not empty
                while ((line = file.ReadLine()) != null)
                {
                    //read, split and store data in tempData object
                    string[] lineData = line.Split(',');
                    DateTime date = DateTime.ParseExact(lineData[2],
                                    "yyyyMMdd",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None);
                    double temp = int.Parse(lineData[3]) / 10;
                    TempData x = new TempData(date, temp, Thread.CurrentThread.Name.ToString());

                    //counting the lines
                    lineReadInCount++;
                    //adding to common list and dispaying data
                    this.Dispatcher.Invoke(new Action(delegate
                    {
                        data.Add(x);
                        ProgressBar1.Value = ((float)data.Count / (float)324363) * 100; //324363
                        lblProgress.Content = "Rows read in by Threads: " + data.Count;
                    }));
                    //pluse and realse lock for the next thread
                    Monitor.Pulse(syncObj);
                    Monitor.Wait(syncObj);
                }
                //display message
                MessageBox.Show(String.Format("Thread {0} read in {1} lines", Thread.CurrentThread.Name, lineReadInCount));

            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
            finally
            {
                Monitor.Exit(syncObj);
                //set to true
                threadBusy = false;
            }

        }

        private void BntStart_Click(object sender, RoutedEventArgs e)
        {
            //thread one and two
            Thread ReadOne = new Thread(new ThreadStart(ReadTempData));
            Thread ReadTwo = new Thread(new ThreadStart(ReadTempData));
            //thread name
            ReadOne.Name = "readOne thread";
            ReadTwo.Name = "readTwo thread";
            //thread start
            ReadOne.Start();
            ReadTwo.Start();
            //thread background property is true
            ReadOne.IsBackground = true;
            ReadTwo.IsBackground = true;
            DataGrid1.ItemsSource = data;
        }

        private void bkw_DoWork(object sender, DoWorkEventArgs e)
        {
            double average_temp = 0;
            //checking if read threads are running
            if (threadBusy)
            {
                e.Cancel = true;
                MessageBox.Show("Wait until all data is read in");
            }
            else
            {
                //get the index passed as parameter to the Background Worker
                int index = (int)e.Argument;

                switch (index)
                {
                    //selecting the season
                    case 0:
                        average_temp = CalculateAverage(0, 3, e);
                        break;
                    case 1:
                        average_temp = CalculateAverage(3, 6, e);
                        break;
                    case 2:
                        average_temp = CalculateAverage(6, 9, e);
                        break;
                    case 3:
                        average_temp = CalculateAverage(9, 12, e);
                        break;
                    default:
                        break;
                }
                //check for cancellation
                if (bkw.CancellationPending == true || average_temp == 0)
                {
                    e.Cancel = true;
                    return;
                }
                //return results
                e.Result = average_temp;
            }

        }
        //calculates average tempterature per season
        private double CalculateAverage(int start, int end, DoWorkEventArgs e)
        {
            double total_temp = 0;
            int count_days = 0;
            int iteration = 0,count=0;
            for (int i = start; i < end; i++)
            {
                iteration = 0;
                count++;
                foreach (var item in data)
                {
                    iteration++;
                    if (bkw.CancellationPending == true)
                    {
                        return 0;
                    }

                    if (item.Date.Month == i)
                    {
                        total_temp += item.TempValue;
                        count_days++;
                    }
                    //dispaying percent complete
                    this.Dispatcher.Invoke(new Action(delegate
                    {
                        //getting percentage complete
                        float percent = (((float)iteration / (float)data.Count) * 100)/3;
                        percent = percent * count;
                        lblpercentComplete.Content = "Percent complete: " + percent + "%";
                    }));
                }
                //thread sleep to delay it for a little bit
                Thread.Sleep(40);
                
            }
            return total_temp / count_days;

        }
        private void BntAverageSeasons_Click(object sender, RoutedEventArgs e)
        {
            lblpercentComplete.Content = "Percent complete: ";
            int index = ComboSeasons.SelectedIndex;
            //parameter passing to thread
            bkw.RunWorkerAsync(argument: index);
        }
        private void bkw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
        }
        private void bkw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // check for error
            if (e.Error != null)
            {
                MessageBox.Show("Error");
            }
            // check if cancel happened
            else if (e.Cancelled)
            {
                MessageBox.Show("Calncelled seasons average calculation");
            }
            else
            {
                // retrieve the result (or value) passed from the DoWork event handler
                double averageTemp = (double)e.Result;
                lblResult.Content = String.Format("Average of the years: {0:f2}", averageTemp);
                MessageBox.Show("Background worker completed");// show messagebox when done  
            }
        }

        private void BntStop_Click(object sender, RoutedEventArgs e)
        {
            //stop thread
            bkw.CancelAsync();
        }

        private void BntSaveISO_Click(object sender, RoutedEventArgs e)
        {
            IsloatedStorage IsloatedStorage1 = new IsloatedStorage();

            //check if the text box has some text into it
            if (lblResult.Content != null)
            {
                Thread writeToISO= new Thread(new ParameterizedThreadStart(IsloatedStorage1.writeToStorage));

                writeToISO.Start(lblResult.Content);
            }
            else
                MessageBox.Show("Please specify a colour !");
        }
    }
    public class TempData
    {
        public DateTime Date { get; set; }
        public double TempValue { get; set; }
        public string Tname { get; set; }
        public TempData(DateTime date, double temp, string name)
        {
            Date = date;
            TempValue = temp;
            Tname = name;
        }
    }
    class IsloatedStorage
    {
        public static Object synObj = new Object();
        private IsolatedStorageFile store;
        //name of folder in isolated store
        private string folderName;
        //the path to the file
        private string pathToTextFile;
        //constructor method which creates the isolated storage
        public IsloatedStorage()
        {
            folderName = "SeasonsISO";
            //set the path to the text file
            pathToTextFile = String.Format("{0}\\AverageSeasons.txt", folderName);
            store = IsolatedStorageFile.GetUserStoreForAssembly();
        }
        //method which writes to Isolated store 
        public void writeToStorage(Object averageSeasonTemp)
        {
            string averageSeasonTempISO = averageSeasonTemp.ToString();
            //check if the isolated store was obtained successfully
            if (store != null)
            {
                lock (synObj)
                {
                    try
                    {
                        //check if the folder exists.If it does not, then create it
                        if (!store.DirectoryExists(folderName))
                            store.CreateDirectory(folderName);

                        using (IsolatedStorageFileStream isoStorageTxtFile =
                            store.OpenFile(pathToTextFile, FileMode.Create, FileAccess.Write))
                        {
                            using (StreamWriter writer = new StreamWriter(isoStorageTxtFile))
                            {
                                writer.Write(averageSeasonTempISO);
                                MessageBox.Show("Data saved to AverageSeasons.txt");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show(e.Message);
                    }
                }
            }
        }
    }
}
