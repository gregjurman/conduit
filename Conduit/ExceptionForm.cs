using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

namespace Conduit
{
    public partial class ExceptionForm : Form
    {
        public ExceptionForm()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Clipboard.SetText(textBox1.Text);
            MessageBox.Show("The error report has been copied to your clipboard. Please log into JIRA and click 'Create Issue'. Paste the report into the Discription box.");

            Process.Start("https://jira.imahi.net");
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
