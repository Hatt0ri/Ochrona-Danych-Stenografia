using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.WinForms;

namespace Ochrona_Danych_Stenografia
{

    public partial class Form1 : Form
    {

        //
        // Form1 (Global) Variables
        //
        LiveCharts.WinForms.PieChart Chart1;
        //Destanation used image

        byte[] dest, dBackup;
        string destPath = "";
        int[] dSize = new int[2];   //  width, height
        int cPaddingB = 0;  //  count of  padding bytes per row
        int lbMarker;   //  last bit marker
        int MBOffset = 38;  //  Start byte for marker ( 7 bytes long )
        int bmpMBsize = 0;
        int lastByteSize = 1;// bits
        bool STOWED = false;
        bool EXTRACTED = false;
        bool spaceGood = false;
        byte MARKER_KEY = 191;
        //File to code

        byte[] src;
        byte[] extr;
        string srcPath = "";
        int sLen=0;
        string savePath = "";

        int cR =7, cG =7 , cB = 7;   //  sliders 1-8

        //  Capacity info (bytes)
        double percFree = 50.0;
        double percUsed = 50.0;
        int usedSpace = 0;
        int freeSpace = 0;

        //  Extracted file vars
        //  1 byte4marker, 2 bytes4 RGBsliders and lastbyteSize, 4 bytes4 last byteOFFset
        int lcR, lcG, lcB;

        //lastNyteSize
        //lbMarker


        //
        public bool ReadMarker()
        {
            // bmp image in dest array 
            if( dest[MBOffset] != MARKER_KEY)
            {
                MessageBox.Show("Image file has no stowed data.", "No marker found.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            byte sr, sg, sb, bb;

            sg = dest[MBOffset + 1];    //  lub % 9 lub /16
            sg = (byte)((int)sg << 4);
            sg = (byte)((int)sg >> 4);
            sr = (byte)((int)dest[MBOffset + 1] >> 4);
            sb = (byte)((int)dest[MBOffset + 2] >> 4);
            trackBar1.Value=lcR = sr;
            trackBar2.Value = lcG = sg;
            trackBar3.Value = lcB = sb;

            bb = (byte)(dest[MBOffset + 2] << 4);
            lastByteSize = bb >> 4;
            //lbMarker = (int)dest[MBOffset + 3];

            byte[] tmp = { dest[MBOffset + 3], dest[MBOffset + 4], dest[MBOffset + 5], dest[MBOffset +6] };
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tmp);

            lbMarker = BitConverter.ToInt32(tmp, 0);

            return true;
        }

        public void BurnMarker()
        {
            //  1 byte4marker, 2 bytes4 RGBsliders and lastbyteSize, 4 bytes4 last byteOFFset
            //offsetformula= sLen*(24/R+G+B) + 54 + 7 + cPaddingB*dSize[1];
            GetColorValues();

            int tmp = 0;
            byte first = MARKER_KEY;   //  marker value
            byte sec = 0;
            byte th = 0;

            tmp = cR << 4;
            sec = (byte)tmp;
            sec += (byte)cG;
            tmp = cB << 4;
            th = (byte)tmp;
            th += (byte)lastByteSize;

                dest[MBOffset] = first;
                dest[MBOffset + 1] = sec;
                dest[MBOffset + 2] = th;

            byte[] bytes = new byte[4];
            bytes[0] = (byte)(lbMarker >> 24);
            bytes[1] = (byte)(lbMarker >> 16);
            bytes[2] = (byte)(lbMarker >> 8);
            bytes[3] = (byte)lbMarker;

            for (int i=0; i < bytes.Length;i++) //  burning lastbyteoffset
            {
                dest[MBOffset + 3 + i] = bytes[i];
            }

        }

        public void GetColorValues()
        {
            cR = trackBar1.Value;
            cG = trackBar2.Value;
            cB = trackBar3.Value;
        }

        public void IncColor(ref int color, ref int coffset)    //  increment
        {
            color++;
            if (color == 3) { color = 0; }

            switch(color)
            {
                case 0:
                    coffset = cR;
                    break;
                case 1:
                    coffset = cG;
                    break;
                case 2:
                    coffset = cB;
                    break;
            }
        }
        public void IncColorX(ref int color, ref int coffset)    //  increment
        {
            color++;
            if (color == 3) { color = 0; }

            switch (color)
            {
                case 0:
                    coffset = lcR;
                    break;
                case 1:
                    coffset = lcG;
                    break;
                case 2:
                    coffset = lcB;
                    break;
            }
        }

        //
        //  MAIN PART OF CODE- STOWING DATA TO BMP  *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *
        //
        public void Stow()
        {
            button2.PerformClick();
            if (spaceGood!=true)
            {
                //MessageBox.Show("Move trackbars!", "You have changed the IMG!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (dSize[0] == 0 || dSize[1] == 0)
            {
                MessageBox.Show("BMP file size was not read!", "Error within the GetImgSize()!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            label4.Text =  lbMarker.ToString();

            int cType= 0;   //  0-R, 1-G, 2-B
            int clo = cR;    //  color iterator
            //int colorSum = cR + cG + cB;
            int dOffset = 55 + bmpMBsize;    //  (byte)
            int sOffset = 0;
            Queue<int> cont = new Queue<int>(8);
            int[] dBits = new int[8];

            //lock sliders
            trackBar1.Enabled = false;
            trackBar2.Enabled = false;
            trackBar3.Enabled = false;

            bool kon = true;    //  var hold loop or drop it
            //division
            //img
            for (int row = 1; row <= dSize[1]; row++)                             //4 evry row
            {
 
                for (int p=0;p < dSize[0];p++)                                  //4 every pixel 3b
                {
                    for (int ib = 0; ib < 3; ib++)                             //  4   every byte
                    {
                        if (sOffset < sLen)
                        {
                            int[] inttmp = new int[8];
                            inttmp = Crr.GetBitArray(src[sOffset++]);
                            for (int a = 0; a < 8; a++)
                            {
                                cont.Enqueue(inttmp[a]);
                            }
                        }
                        //=======================
                        if (clo > cont.Count)
                        {
                            kon = false;
                            if (cont.Count == 0)
                            {
                                lbMarker = dOffset - 1;
                            }
                            else
                            {
                                lbMarker = dOffset;
                            }
                            lastByteSize = clo = cont.Count;    //  if clo is 0 then byte uses value of trackbar
                        }

                        dBits = Crr.GetBitArray(dest[dOffset]);
                        for (int C = 8 - clo; C <= 7; C++)
                        {
                            dBits[C] = cont.Dequeue();
                        }
                        dest[dOffset] = Crr.GetByte(dBits); 
                        if (!kon) { break; }
                        dOffset++;
                        IncColor(ref cType, ref clo);   //  increment color
                    }
                    if (!kon) { break; }
                }
                if( !kon) {break;}

                dOffset += cPaddingB;
            }

            BurnMarker();

            //unlock sliders
            trackBar1.Enabled = true;
            trackBar2.Enabled = true;
            trackBar3.Enabled = true;
            GetColorValues();

            //  code 4 img preview
            Bitmap bmp;
            using (var ms = new MemoryStream(dest))
            {
                bmp = new Bitmap(ms);
            }
            pictureBox2.Image = bmp;
            pictureBox2.Refresh();
            //bmp.Dispose();
            label4.Text += ", "+lbMarker.ToString();


        }
        //  ===================================================================================================

        public void Extract()
        {
            //  vars
            //if(src != null) Array.Clear(src, 0, src.Length);
            //src = null;

            int cType = 0;   //  0-R, 1-G, 2-B
            int clo = lcR;    //  color iterator
            //int colorSum = cR + cG + cB;
            int dOffset = 55 + bmpMBsize;    //  (byte)
            int sOffset = 0;
            Queue<int> cont = new Queue<int>(8);
            int[] dBits = new int[8];
            //  list 4 test
            List<byte> lFinal = new List<byte>(dest.Length);

            bool kon = true;    //  var hold loop or drop it


            for (int row = 1; row <= dSize[1]; row++)                             //4 evry row
            {

                for (int p = 0; p < dSize[0]; p++)                                  //4 every pixel 3b
                {
                    for (int ib = 0; ib < 3; ib++)                             //  4   every byte
                    {
                        //=======================
                        if (lbMarker== dOffset) //  if last byte to read
                        {
                            if( lastByteSize!=0)
                            {
                                clo = lastByteSize;
                            }
                            kon = false;
                        }

                        dBits = Crr.GetBitArray(dest[dOffset]);
                        for (int C = 8 - clo; C <= 7; C++)
                        {
                            cont.Enqueue(dBits[C]);
                        }
                        dOffset++;
                        IncColorX(ref cType, ref clo);   //  increment color

                        if (cont.Count >= 8)
                        {
                            for (int a = 0; a < 8; a++)
                            {
                                dBits[a] = cont.Dequeue();
                            }
                            //src[sOffset++] = Crr.GetByte(dBits);
                            sOffset++;
                            lFinal.Add(Crr.GetByte(dBits));
                        }
                        
 
                        if (!kon) { break; }
                    }
                    if (!kon) { break; }
                }
                if (!kon) { break; }
                dOffset += cPaddingB;
            }

            EXTRACTED = true;
            extr = new byte[sOffset];
            /*for(int i=0;i<sOffset;i++)
            {
                extr[i] = new byte();
            }*/
            System.Buffer.BlockCopy(lFinal.ToArray(), 0, extr, 0, sOffset);
            lFinal.Clear();
            //Array.Copy(src, extr, sOffset);
            //src = new byte[0];
            textBox2.Text= srcPath = "";
        }
        //  *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *   *

        public static byte[] ReadFully(Stream input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }


        public static int[] GetImgSize( byte[] des, out int padd )   //  ( int )Width, Height
        {
            if( des.Length <= 54)
            {
                padd = 0;
                return null;
            }

            int[] arr= new int[2];
            if( (int)des[18] == 0)
            {
                arr[0]=arr[1]=(int)Math.Sqrt((des.Length - 54) / 3);
            }
            else
            {
                arr[0] = (int)des[18];
                arr[1] = (int)des[22];
            }

            padd = (arr[0] * 3) % 4;
            return arr;
        }


        public bool CheckSpace()    //  Free space to encode
        {
            pictureBox2.Image = null;
            if (destPath == "" || srcPath == "")
            {
                MessageBox.Show("Did not specified files' paths!", "No specified paths!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;   
            }
            GetColorValues();

            //int tmp = sLen * (int)Math.Ceiling(((double)24 / (cR + cG + cB))) + 54 + bmpMBsize;    //  offsetformula
            //int tmp = (int)Math.Ceiling((double)sLen * (24 / (double)(cR + cG + cB)));    //  offsetformula
            int tmp = sLen * (int)Math.Ceiling((double)(24 / (cR + cG + cB)));
            tmp += 54 + bmpMBsize + (sLen / (dSize[0] * 3)) * cPaddingB;
            lbMarker = tmp;

            
            if( dest.Length <= tmp || sLen > dest.Length)
            {
                MessageBox.Show("Picked file to hide is to huge! Try to pick larger carrier image or move trackbars.", "File to large!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            //  % sdfsdfsdfsdf
            tmp = sLen * (int)Math.Ceiling((double)(24 / (cR + cG + cB)));
            usedSpace = tmp + bmpMBsize;
            tmp = dSize[0] * dSize[1] * 3;
            freeSpace = tmp - usedSpace;
            percUsed = 100 * usedSpace / tmp;
            percFree = 100 - percUsed;

            if(freeSpace < 0) {
                MessageBox.Show("Picked file to hide is to huge! Try to pick larger carrier image or move trackbars.", "File to large!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }


        public Form1()
        {
            InitializeComponent();
            //panel1
            panel1.Size = new Size(380, 280);
            this.MaximumSize = new Size(382, 341);  //  Start size
            //piechart
            //Form1.
            LiveCharts.WinForms.PieChart chart1 = new LiveCharts.WinForms.PieChart();
            Chart1 = chart1;
            Chart1.Location = panel8.Location;
            Chart1.Size = panel8.Size;
            panel2.Controls.Add(Chart1);

            panel1.MinimumSize = panel1.Size;
            bt3.Enabled = false;
            bt2.Enabled = false;
            

        }

        //  Button to select file to encode
        private void bt2_Click(object sender, EventArgs e)
        {
            Stream ms = null;
            
            FC.Filter = "All files(*.*) |*.*";
            if (FC.ShowDialog() == DialogResult.OK)
            {
                srcPath= textBox2.Text = FC.FileName;

                try
                {
                    if ((ms = FC.OpenFile()) != null)
                    {
                        using (ms)
                        {
                            src = ReadFully(ms);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file(FC) from disk. Original error: " + ex.Message);
                }

                ms.Dispose();
                ms.Close();

                sLen = src.Length;
                textBox3.Text += "\r\nFile Size: " + sLen.ToString();
                this.Size = this.MaximumSize = new Size(770, 772);  //  Start size

                button2.PerformClick();


            }
        }

      
        private void button2_Click(object sender, EventArgs e)  //  Check button
        {
            STOWED = false;
            button3.Enabled = false;    //save
            bt3.Enabled = false;
            if (spaceGood=CheckSpace())
            {
                bt3.Enabled = true;
            }
            textBox3.Text += "\r\nlboffset: " + lbMarker.ToString();
            // chart
            //LiveCharts.Definitions.Series.IPieSeries pieSeries;
            //Chart1.Series[0].Values = 
            Func<ChartPoint, string> labelPoint = chartPoint =>
               string.Format("{0} ({1:P})", chartPoint.Y, chartPoint.Participation);

            if (Chart1.Series.Count != 0)
            {
                Chart1.Series.Clear();
            }
            Chart1.Series = new SeriesCollection
            {
            new PieSeries
            {
                Title = "Used",
                Values = new ChartValues<double> {usedSpace},
                PushOut = 5,
                DataLabels = true,
                LabelPoint = labelPoint
            },
            new PieSeries
            {
                Title = "Free",
                Values = new ChartValues<double> {freeSpace},
                DataLabels = true,
                LabelPoint = labelPoint
            },
        };

            Chart1.LegendLocation = LegendLocation.Bottom;

            textBox3.Text = "\rWidth:" + dSize[0].ToString() + ",Height:" + dSize[1].ToString();
            textBox3.Text += "\r\nBMP Size: " + dest.Length.ToString();
            textBox3.Text += "\r\nCapacity: " + (dSize[0] * dSize[1]*3).ToString();
            textBox3.Text += "\r\nFile Size: " + sLen.ToString();
            textBox3.Text += "\r\nUsed: " + percUsed.ToString() + " %" + " , " + usedSpace.ToString();
            textBox3.Text += "\r\nFree: " + percFree.ToString() + " %" + " , " + freeSpace.ToString();
        }

        private void cB1_CheckedChanged(object sender, EventArgs e)
        {
            if (cB1.Checked)
            {
                EXTRACTED = false;
                textBox4.Text=savePath = "";
                label2.Visible = false;
                textBox2.Visible = false;
                bt2.Visible = false;
                bt3.Visible = false;
                this.MaximumSize = new Size(382, 341);  //  Start size

                if (destPath != "")
                {
                    if (ReadMarker())
                    {
                        textBox3.Text = "File has hidden data to extract!\r\nChoose path to save the file and click 'Save'!";
                    }
                    else
                    {
                        textBox3.Text = "No marker found. File has no data to extract!";
                    }
                }
            }
            else
            {
                label2.Visible = true;
                textBox2.Visible = true;
                bt2.Visible = true;
                bt3.Visible = true;
                this.Size = this.MaximumSize = new Size(770, 772);  //  Start size
            }
        }

        private void bt3_Click(object sender, EventArgs e)
        {
            using (var mms = new MemoryStream(dBackup))
            {
                dest = null;
                dest = ReadFully(mms);
            }
            pictureBox2.Image = pictureBox1.Image;
            pictureBox2.Refresh();
            //  can put timer here
            Stow();
            STOWED = true;
            button3.Enabled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (savePath=="")
            {
                MessageBox.Show("Error: Could not save file! Specify your file's path!");
                return;
            }
            if( STOWED)
            {
                try
                {
                    File.WriteAllBytes(savePath, dest);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not save file. Original error: " + ex.Message);
                }
            }
            else if (EXTRACTED)
            {
                try
                {
                    File.WriteAllBytes(savePath, extr);
                  /*  using (BinaryWriter writer = new BinaryWriter(File.Open(savePath, FileMode.Create)))
                    {
                        writer.Write(extr);
                    }*/
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not save file. Original error: " + ex.Message);
                }
            }
        }

        //  Button to select dest BMP
        private void bt1_Click(object sender, EventArgs e)
        {
            if (dest != null) Array.Clear(dest, 0, dest.Length);

            Stream ms = null;
            bt3.Enabled = false;
            bt2.Enabled = false;
            this.MaximumSize = new Size(382, 341);  //  Start size

            OFD.Filter= "BMP files(*.bmp) |*.bmp";
            if (OFD.ShowDialog() == DialogResult.OK)
            {
                destPath= textBox1.Text = OFD.FileName;

                try
                {
                    if ((ms = OFD.OpenFile()) != null)
                    {
                        using (ms)
                        {
                            if (dest != null) Array.Clear(dest, 0, dest.Length);

                            dest = ReadFully(ms);
                            ms.Seek(0, SeekOrigin.Begin);
                            dBackup = ReadFully(ms);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Could not read file(OFD) from disk. Original error: " + ex.Message);
                }

                ms.Dispose();
                ms.Close();

                dSize = GetImgSize(dest,out cPaddingB);
                
                bt2.Enabled = true;

                Bitmap bmp;
                using (var mms = new MemoryStream(dest))
                {
                    bmp = new Bitmap(mms);
                }
                pictureBox1.Image = bmp;
                pictureBox1.Refresh();
            }
        }

        //  Button  to select save path
        private void button1_Click(object sender, EventArgs e)
        {
            if (!cB1.Checked)
            {
                SFD.Filter = "Bmp files(*.bmp) |*.bmp";
            }
            else
            {
                SFD.Filter = "All files(*.*) |*.*";
                Extract();
                button3.Enabled = true;
            }
            if( destPath != "")
            {
                SFD.FileName = destPath.Remove(destPath.LastIndexOf('.'));
            }
            SFD.ShowDialog();

            if (SFD.FileName != "")
            {
                textBox4.Text= savePath = SFD.FileName;
            }
        }
    }
}
