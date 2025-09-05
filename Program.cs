using EcoChemie.Autolab.Extensions.Widgets;
using EcoChemie.Autolab.Sdk;
using EcoChemie.Autolab.Sdk.MultiAutolab;
using EcoChemie.Autolab.SDK.Extensions;
using EcoChemie.Autolab.Sdk.Nova;
using EcoChemie.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using EI = EcoChemie.Autolab.Sdk.EI;
using Instrument = EcoChemie.Autolab.Sdk.Instrument;

namespace GEIS_charge_discharge_Autolab_ConsoleApp_03
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // print mode information
            Console.WriteLine("Autolab is operating in the galvanostatic mode.");
            // print offset/DC current 
            Console.WriteLine($"The offset current is: {args[0]}");
            float offset_current = float.Parse(args[0]);
            // FRA amplitude current
            Console.WriteLine($"The amplitude current is: {args[1]}");
            float amp_current = float.Parse(args[1]);
            // EIS starting exponent of frequency
            Console.WriteLine($"The starting exponent of frequency is: {args[2]}");
            float freq_exp_start = float.Parse(args[2]);
            // EIS stop exponent of frequency
            Console.WriteLine($"The stop exponent of frequency is: {args[3]}");
            float freq_exp_stop = float.Parse(args[3]);
            // EIS exponent step of frequency
            Console.WriteLine($"The exponent step of frequency is: {args[4]}");
            float freq_exp_step = float.Parse(args[4]);
            // define minimum integration cycles
            Console.WriteLine($"The minimum integration cycles are: {args[5]}");
            int min_integ_cycles = int.Parse(args[5]);
            // define minimum integration time
            Console.WriteLine($"The minimum integration time is: {args[6]}");
            float min_integ_time = float.Parse(args[6]);
            // define maximum voltage threshold for galvanostatic charging
            Console.WriteLine($"The maximum voltage threshold in charging is: {args[7]}");
            float vol_max = float.Parse(args[7]);
            // define minimum voltage threshold in galvanostatic discharging 
            Console.WriteLine($"The minimum voltage threshold in discharging is: {args[8]}");
            float vol_min = float.Parse(args[8]);
            // define maximum number of cycles
            Console.WriteLine($"The maximum number of cycles is: {args[9]}");
            int max_no_cycles = int.Parse(args[9]);

            // calculate number of frequency points
            int no_freq_points = (int)Math.Abs((freq_exp_stop - freq_exp_start) / freq_exp_step) + 1;

            // initialize frequency array
            float[] freq_exp_array = new float[no_freq_points];
            // fill frequency array
            for (int index_0 = 0; index_0 < no_freq_points; index_0++)
            {
                freq_exp_array[index_0] = freq_exp_start + index_0 * freq_exp_step;
            }

            // initialize EIS array
            double[,] eis_array = new double[no_freq_points, 6];

            // save voltage data
            // path to voltage data
            string vol_filename = @"c:\GEIS_data\voltage_data.txt";
            // create object StreamWriter for voltage data
            StreamWriter volw = System.IO.File.AppendText(vol_filename);

            // connect to first Autolab instrument
            InstrumentConnectionManager autolab_manager = new InstrumentConnectionManager(@"C:\Program Files\Metrohm Autolab\Autolab SDK 2.1\Hardware Setup Files");
            var my_instrument = autolab_manager.ConnectToFirstInstrument();
            Console.WriteLine("The program is now connected to the first Autolab instrument.");

            // query if my instrument has FRA module
            bool has_fra_module = my_instrument.HasFraModule();
            // print response on the screen
            if (has_fra_module == true)
            {
                Console.WriteLine("The Autolab has FRA module.");
            }
            else
            {
                Console.WriteLine("Autolab has no FRA module.");
            }

            // Set the instrument to be used by the kernel
            my_instrument.SetAsDefault();
            // print serial number of Autolab with FRA module
            Console.WriteLine($"Fra sdk-measurement on {my_instrument.GetSerialNumber()}");

            // set mode to galvanostatic mode for GEIS
            my_instrument.Ei.Mode = EI.EIMode.Galvanostatic;

            // Select the appropriate CurrentRange
            my_instrument.Ei.CurrentRange = EI.EICurrentRange.CR10_1mA;
            // set high stability measurement
            my_instrument.Ei.Bandwidth = EI.EIBandwidth.High_Stability;

            // read current voltage or potential
            double vol_current = my_instrument.Ei.Potential;
            // condition for charging or discharging
            string direction = string.Empty;
            if (vol_current < vol_max)
            {
                Console.WriteLine("Do battery charging.");
                direction = "charging";
                // set offset/DC curent
                my_instrument.Ei.Setpoint = offset_current;
            }
            else
            {
                Console.WriteLine("Do battery discharging.");
                direction = "discharging";
                // set offset/DC curent
                my_instrument.Ei.Setpoint = (-1.0f)*offset_current;
            }

            // set FRA parameters
            my_instrument.Fra.Amplitude = amp_current;
            my_instrument.Fra.WaveType = FraWaveType.Sine;
            my_instrument.Fra.MinimumIntegrationCycles = min_integ_cycles;
            my_instrument.Fra.MinimumIntegrationTime = min_integ_time;

            // switch on the Fra-DSG relay on the PGSTAT
            my_instrument.Ei.EnableDsgInput = true;
            // switch on eletrochemical cell of Autolab
            my_instrument.Ei.CellOnOff = EI.EICellOnOff.On;

            Console.WriteLine("Measuring GEIS now...");
            // Switch on the FRA
            my_instrument.SwitchFraOn();

            // wait for 1 second to stabilize after switching the cell on
            System.Threading.Thread.Sleep(1000);

            // initialize counter
            int counter = 0;
            // main while loop
            while ((direction == "charging" || direction == "discharging") && (counter < 2*max_no_cycles))
            {
                if (direction == "charging")
                {
                    // set offset/DC curent
                    my_instrument.Ei.Setpoint = offset_current;
                    // wait 1 s
                    System.Threading.Thread.Sleep(1000);

                    for (int index_0 = 0; index_0 < no_freq_points; index_0++)
                    {
                        if (index_0 == 0)
                        {
                            Console.WriteLine($"The potential at the beginning of EIS is: {my_instrument.Ei.Potential}");
                            // read current voltage or potential
                            vol_current = my_instrument.Ei.Potential;
                            // convert current volatge to string
                            string vol_current_str = vol_current.ToString();
                            // condition
                            if (vol_current < vol_max)
                            {
                                // write current voltage to text file
                                volw.WriteLine(vol_current_str);
                                // update charging / discharging direction
                                direction = "charging";
                                // set FRA frequency
                                double freq_current = Math.Pow(10, freq_exp_array[index_0]);
                                freq_current = Math.Round(freq_current, 3);
                                my_instrument.Fra.Frequency = freq_current;
                                // wait 5 s
                                System.Threading.Thread.Sleep(5000);
                                // perform measurement by starting FRA
                                my_instrument.Fra.Start();
                                // print end of FRA measurement
                                //Console.WriteLine("Measurement finished.");
                                // measure EIS, impedance
                                double Z_freq = my_instrument.Fra.Frequency;
                                double Z_total = my_instrument.Fra.Modulus[0];
                                double Z_phase = my_instrument.Fra.Phase[0];
                                double Z_real = my_instrument.Fra.Real[0];
                                double Z_imag = my_instrument.Fra.Imaginary[0];
                                double Z_time = my_instrument.Fra.TimeData[0];
                                // fill EIS array
                                eis_array[index_0, 0] = Z_freq;
                                eis_array[index_0, 1] = Z_real;
                                eis_array[index_0, 2] = Z_imag;
                                eis_array[index_0, 3] = Z_total;
                                eis_array[index_0, 4] = Z_phase;
                                eis_array[index_0, 5] = Z_time;
                                // increment counter
                                counter += 1;
                            }
                            else
                            {
                                // update charging / discharging direction
                                direction = "discharging";
                                // break for cycle
                                break;
                            }
                        }
                        else
                        {
                            // set FRA frequency
                            double freq_current = Math.Pow(10, freq_exp_array[index_0]);
                            freq_current = Math.Round(freq_current, 3);
                            my_instrument.Fra.Frequency = freq_current;
                            // wait 1 s
                            System.Threading.Thread.Sleep(1000);
                            // perform measurement by starting FRA
                            my_instrument.Fra.Start();
                            // print end of FRA measurement
                            //Console.WriteLine("Measurement finished.");
                            // measure EIS, impedance
                            double Z_freq = my_instrument.Fra.Frequency;
                            double Z_total = my_instrument.Fra.Modulus[0];
                            double Z_phase = my_instrument.Fra.Phase[0];
                            double Z_real = my_instrument.Fra.Real[0];
                            double Z_imag = my_instrument.Fra.Imaginary[0];
                            double Z_time = my_instrument.Fra.TimeData[0];
                            // fill EIS array
                            eis_array[index_0, 0] = Z_freq;
                            eis_array[index_0, 1] = Z_real;
                            eis_array[index_0, 2] = Z_imag;
                            eis_array[index_0, 3] = Z_total;
                            eis_array[index_0, 4] = Z_phase;
                            eis_array[index_0, 5] = Z_time;

                            // condition to save data
                            if (index_0 == (no_freq_points - 1))
                            {
                                // save EIS data
                                // root folder
                                string root_folder = @"c:\GEIS_data";
                                // filename
                                string filename = "GEIS_scan";
                                // convert counter to string
                                string counter_str = counter.ToString().PadLeft(6, '0');
                                // build full file name
                                string filename_full = filename + "_" + counter_str + ".txt";
                                // build full path to file
                                string full_path = Path.Combine(root_folder, filename_full);

                                // create object StreamWriter
                                StreamWriter sw = new StreamWriter(full_path);

                                // main for loop to read pixel values
                                for (int index_1 = 0; index_1 < no_freq_points; index_1++)
                                {
                                    for (int index_2 = 0; index_2 < 6; index_2++)
                                    {
                                        sw.Write(eis_array[index_1, index_2] + "\t");
                                    }
                                    sw.Write("\n");
                                }
                            }
                        }
                    }
                }
                else if (direction == "discharging")
                {
                    // set offset/DC curent
                    my_instrument.Ei.Setpoint = (-1.0f) * offset_current;
                    // wait 1 s
                    System.Threading.Thread.Sleep(1000);

                    for (int index_0 = 0; index_0 < no_freq_points; index_0++)
                    {
                        if (index_0 == 0)
                        {
                            Console.WriteLine($"The potential at the beginning of EIS is: {my_instrument.Ei.Potential}");
                            // read current voltage or potential
                            vol_current = my_instrument.Ei.Potential;
                            // convert current volatge to string
                            string vol_current_str = vol_current.ToString();
                            // condition
                            if (vol_current > vol_min)
                            {
                                // write current voltage to text file
                                volw.WriteLine(vol_current_str);
                                // update charging / discharging direction
                                direction = "discharging";
                                // set FRA frequency
                                double freq_current = Math.Pow(10, freq_exp_array[index_0]);
                                freq_current = Math.Round(freq_current, 3);
                                my_instrument.Fra.Frequency = freq_current;
                                // wait 5 s
                                System.Threading.Thread.Sleep(5000);
                                // perform measurement by starting FRA
                                my_instrument.Fra.Start();
                                // print end of FRA measurement
                                //Console.WriteLine("Measurement finished.");
                                // measure EIS, impedance
                                double Z_freq = my_instrument.Fra.Frequency;
                                double Z_total = my_instrument.Fra.Modulus[0];
                                double Z_phase = my_instrument.Fra.Phase[0];
                                double Z_real = my_instrument.Fra.Real[0];
                                double Z_imag = my_instrument.Fra.Imaginary[0];
                                double Z_time = my_instrument.Fra.TimeData[0];
                                // fill EIS array
                                eis_array[index_0, 0] = Z_freq;
                                eis_array[index_0, 1] = Z_real;
                                eis_array[index_0, 2] = Z_imag;
                                eis_array[index_0, 3] = Z_total;
                                eis_array[index_0, 4] = Z_phase;
                                eis_array[index_0, 5] = Z_time;
                                // increment counter
                                counter += 1;
                            }
                            else
                            {
                                // update charging / discharging direction
                                direction = "charging";
                                // break for cycle
                                break;
                            }
                        }
                        else
                        {
                            // set FRA frequency
                            double freq_current = Math.Pow(10, freq_exp_array[index_0]);
                            freq_current = Math.Round(freq_current, 3);
                            my_instrument.Fra.Frequency = freq_current;
                            // wait 1 s
                            System.Threading.Thread.Sleep(1000);
                            // perform measurement by starting FRA
                            my_instrument.Fra.Start();
                            // print end of FRA measurement
                            //Console.WriteLine("Measurement finished.");
                            // measure EIS, impedance
                            double Z_freq = my_instrument.Fra.Frequency;
                            double Z_total = my_instrument.Fra.Modulus[0];
                            double Z_phase = my_instrument.Fra.Phase[0];
                            double Z_real = my_instrument.Fra.Real[0];
                            double Z_imag = my_instrument.Fra.Imaginary[0];
                            double Z_time = my_instrument.Fra.TimeData[0];
                            // fill EIS array
                            eis_array[index_0, 0] = Z_freq;
                            eis_array[index_0, 1] = Z_real;
                            eis_array[index_0, 2] = Z_imag;
                            eis_array[index_0, 3] = Z_total;
                            eis_array[index_0, 4] = Z_phase;
                            eis_array[index_0, 5] = Z_time;

                            // condition to save data
                            if (index_0 == (no_freq_points - 1))
                            {
                                // save EIS data
                                // root folder
                                string root_folder = @"c:\GEIS_data";
                                // filename
                                string filename = "GEIS_scan";
                                // convert counter to string
                                string counter_str = counter.ToString().PadLeft(6, '0');
                                // build full file name
                                string filename_full = filename + "_" + counter_str + ".txt";
                                // build full path to file
                                string full_path = Path.Combine(root_folder, filename_full);

                                // create object StreamWriter
                                StreamWriter sw = new StreamWriter(full_path);

                                // main for loop to read pixel values
                                for (int index_1 = 0; index_1 < no_freq_points; index_1++)
                                {
                                    for (int index_2 = 0; index_2 < 6; index_2++)
                                    {
                                        sw.Write(eis_array[index_1, index_2] + "\t");
                                    }
                                    sw.Write("\n");
                                }
                            }
                        }
                    }
                }
                else 
                {
                    // break while loop
                    break;
                }

                // stop GEIS measurement script
                // cretae path to stop text file
                string path_to_stop_file = @"c:\GEIS_data\stop.txt";
                // open with shared read/write access
                FileStream fs = new FileStream(path_to_stop_file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                // create stream reader object
                StreamReader sr = new StreamReader(fs);
                // read first line
                string firstLine = sr.ReadLine();
                //Console.WriteLine(firstLine);
                // condition
                if (firstLine == "stop")
                {
                    Console.WriteLine("The main while loop of GEIS measurement is broken.");
                    Console.WriteLine("The GEIS script is stopped");
                    System.Threading.Thread.Sleep(1000);
                    break;
                }
                else 
                {
                    continue;
                }
            }

            // switch off eletrochemical cell of Autolab
            my_instrument.Ei.CellOnOff = EI.EICellOnOff.Off;
            // switch off the FRA relay on the PGSTAT
            my_instrument.Ei.EnableDsgInput = false;

            // Switch off the FRA, good habit to turn things off
            my_instrument.SwitchFraOff();

            // Dispose Autolab manager
            autolab_manager.Dispose();
        }
    }
}
