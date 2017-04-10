using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WaveFormApp
{
    public partial class WaveFormForm : Form
    {
        //initialising function classes as objects
        inputOutput IO = new inputOutput();
        analysis compute = new analysis();
        
        public WaveFormForm()
        {
            InitializeComponent();
        }

        private void sortButton_Click(object sender, EventArgs e)
        {
            //initialise variables
            string fileName;
            double centreLine = 0;
            double[,] waveData;
            double waveFreq;
            List<string> waveFreqs = new List<string>(); //create a blank list
            

            for (int i = 0; i < 1000; i++)//loops through 0 to 999 to cover all possible "WaveXXX.csv" file variations
            {
                //if statement to build filename correctly (avoiding missing/extra 0s), file is expected to be in the same folder as the exe
                if (i > 99)
                    fileName = "Wave" + i + ".csv";
                else if (i > 9)
                    fileName = "Wave0" + i + ".csv";
                else
                    fileName = "Wave00" + i + ".csv";

                try//main functions are error handled incase of missing files between 0 and 999 or an incorrectly formatted file
                {
                    waveData = IO.loadCSV(fileName);

                    centreLine = compute.getMeanLine(waveData);

                    waveFreq = compute.getFreq(waveData, centreLine);

                    waveFreqs = updateResults(waveFreqs, fileName, waveFreq);

                }
                catch
                {

                }
            }

            try//if the results file is currently in use/inaccessible the program will display a message denoting this
            {
                IO.writeCSV(waveFreqs);
                errorLbl.Text = "";
            }
            catch
            {
                errorLbl.Text = "Failed to write results file. Please close file if opened.";
            }

        }

        public List<string> updateResults(List<string> resultsList, string file, double freq)//function for taking the existing list of results and adding the current files result to the list in order
        {
            string newResult = file + ',' + freq.ToString();//create the list entry using the final frequency and filename
            int listSize = resultsList.Count();//get the current list length before new items are added

            for(int i = 0; i <= listSize; i++)//loop until one greater than the index size to allow insertion of an entry at the very end of the table
            {
                if (listSize != 0)//if the list is not empty
                {
                    if (i == listSize)//if the loop has reached the listSize then it has looped through the whole list and the frequency is lower than all other entries so it is added to the end of the list without checking the entry at this index (it doesn't exist)
                    {
                        resultsList.Insert(i, newResult);

                    }
                    else//if the loop has not reached the end of the list the entry at the current index is loaded...
                    {
                        var line = resultsList[i];
                        var values = line.Split(',');

                        if (Convert.ToDouble(values[1]) < freq)//...and its frequency is compared to the new result if it is less then the new result is inserted and the old entry is shifted down
                        {
                            resultsList.Insert(i, newResult);
                            i = listSize;
                        }

                    }
                }
                else//if the list is empty add the first result as the first entry
                {
                    resultsList.Add(newResult);
                }

                
            }

            return resultsList;//the new list is returned
        }
    }

    class analysis
    {
        public double getMeanLine(double[,] waves)//function finds the mean of all voltages in a file
        {
            double mean = 0.0;

            for(int i = 0; i < waves.GetLength(0); i++)//loops through the file and creates the sum of all volts
            {
                mean += waves[i, 1];
            }

            mean = mean / waves.GetLength(0);//divides the sum of all volts by the number of volts to generate the mean (assuming the central line is horizontal and not angled)

            return mean;
        }
    
        public double getFreq(double[,] waves, double meanLine)//get the frequency of a file's wave using the horizontal central line as reference
        {
            //initialise variables
            double newFreq = 0.0;
            double freq = 0.0;
            double timeStart = 0.0;
            bool firstCross = true;
            bool above;
            double tempVal;
            double waveBestAmp = 0.0;
            double greatestAmp = 0.0;
            int cross = 0;

            if ((waves[0, 1] + waves[1,1] + waves[2,1]) / 3 > meanLine)//check to see if the first value will start above or below the central line and stores as bool
                above = true;
            else
                above = false;
            
            for (int i = 0; i < waves.GetLength(0); i++)//loop the length of the array
            {
                if (i>0 && ++i < waves.GetLength(0))//check if current entry is the first or last value, allows for an average to be taken using the adjacent values to limit effect of noise
                {

                    tempVal = (waves[--i, 1] + waves[i, 1] + waves[++i, 1]) / 3;//average of current entry and adjacent values


                    if (above == true && tempVal < meanLine)//two if statements that check whether the wave has crossed the central line and increments the cross value accordingly
                    {
                        cross++;
                        above = false;
                    }
                    if (above == false && tempVal > meanLine)
                    {
                        cross++;
                        above = true;
                    }

                    if (firstCross == true && cross > 0)//if it is the first cross the initial start time must be recorded
                    {
                        firstCross = false;
                        timeStart = waves[i, 0];
                    }

                    if (tempVal > meanLine)//check whether the current voltage is higher or lower than the meanline and calculate the difference accordingly
                        if (tempVal - meanLine > waveBestAmp)
                            waveBestAmp = tempVal - meanLine;
                    if (tempVal < meanLine)
                        if (tempVal + meanLine > waveBestAmp)
                            waveBestAmp = tempVal + meanLine;


                    if (cross > 2)//once the wave crosses the central line twice a full wave has been completed and the frequency can be taken
                    {
                        if(waveBestAmp > greatestAmp)//the highest amplitude achieved in this wave is checked against the previous best and if it is higher the new frequency is accepted as the new highest amp frequency
                        {
                            freq = waves[i, 0] - timeStart;//length of time it took for this wave to complete
                            greatestAmp = waveBestAmp;
                        }
                        waveBestAmp = 0.0;//highest amplitude is reset for new wave
                        timeStart = waves[i, 0];//new start time recorded
                        cross = 1;//as the completion of a wave starts a new wave the first cross has already occured
                    }

                }
                
            }

            return freq;//return the frequency of the wave with the greatest amplitude
        }
    }

    class inputOutput
    {
        public double[,] loadCSV(string fileName)//function for loading in files formated in the way of the WaveXXX.csv files
        {
            var reader = new StreamReader(File.OpenRead(fileName));//loads file to be read in

            List<double> listSecs = new List<double>();//initialise two lists to store the time and voltage for each row of the loaded table
            List<double> listVolts = new List<double>();

            bool header = true;//bool for use in determining whether the header of the table has been iterated past
            while (!reader.EndOfStream)
            {

                var line = reader.ReadLine();
                var values = line.Split(',');

                if (header == false)//if the current row is the header (first line) it will not be read in as it does not contain a double and will not convert properly
                {
                    listSecs.Add(Convert.ToDouble(values[0]));//each row entry is read into its respective list
                    listVolts.Add(Convert.ToDouble(values[1]));
                }
                else
                {
                    header = false;
                }
            }

            double[,] storage;//2d array is created to store the values

            storage = new double[listVolts.Count,2];// length of trhe lists is used to designate the size of the first dimension of the array, there will always be two values per line meaning the second dimension should be two large

            for(int i = 0; i < listSecs.Count; i++)//lists are iterated through and passed into the double array
            {
                storage[i, 0] = listSecs[i];
                storage[i, 1] = listVolts[i];
            }

            return storage;//array of the values in the file is returned
        }

        public void writeCSV(List<string> results)//load a preformated list of strings
        {
            var csv = new StringBuilder();

            for (int i = 0; i < results.Count(); i++)//loop through full set of indices
            {
                csv.AppendLine(results[i]);//add each entry to the end of the file (they are already formated for ",") on its own line
            }
            File.WriteAllText("results.csv", csv.ToString());//write to the results.csv file
        }
    }
}
