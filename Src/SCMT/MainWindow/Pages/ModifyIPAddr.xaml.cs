﻿using System;
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
using UICore.Controls.Metro;

namespace SCMTMainWindow.Pages
{
    /// <summary>
    /// ModifyIPAddr.xaml 的交互逻辑
    /// </summary>
    public partial class ModifyIPAddr : MetroWindow
	{
        public string strNewIPAddr = string.Empty;
        public bool bOK = false;

		public ModifyIPAddr(string strIP)
		{
			InitializeComponent();

            this.CurFName.Text = strIP;
		}

		private void CancelModify_OnClick(object sender, RoutedEventArgs e)
		{
            bOK = false;
			Close();
		}

		private void ConfirmModify_OnClick(object sender, RoutedEventArgs e)
		{
            if(this.NewFName.Text != null && this.NewFName.Text != "")
            {
                strNewIPAddr = this.NewFName.Text;
                bOK = true;
                this.Close();
            }
            else
            {
                MessageBox.Show("请输入新的IP地址");
            }
		}
	}
}