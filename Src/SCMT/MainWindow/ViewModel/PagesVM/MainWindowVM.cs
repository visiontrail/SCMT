﻿/*----------------------------------------------------------------
// Copyright (C) 2019 大唐移动通信设备有限公司 版权所有;
//
// 文件名：MainWindowVM.cs
// 文件功能描述：主界面VM类,主要负责管理所有页签及其行为;
// 创建人：郭亮;
// 版本：V1.0
// 创建时间：2019-1-6
//----------------------------------------------------------------*/

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using ChromeTabs;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

namespace SCMTMainWindow.ViewModel
{
    /// <summary>
    /// 主界面VM层基类;
    /// </summary>
    public class MainWindowVM : ViewModelBase
    {
        /// <summary>
        /// 所有标签页内容集合,ChromeTabs控件，可容纳自定义的类型;
        /// </summary>
        public ObservableCollection<TabBase> ItemCollection { get; set; }

        /// <summary>
        /// 重新排序Tab标签;
        /// </summary>
        public RelayCommand<TabReorder> ReorderTabsCommand { get; set; }

        /// <summary>
        /// 添加Tab页的处理命令;
        /// </summary>
        public RelayCommand AddTabCommand { get; set; }

        /// <summary>
        /// 关闭Tab页的处理命令;
        /// </summary>
        public RelayCommand<TabBase> CloseTabCommand { get; set; }
        
        /// <summary>
        /// 当前选择的Tab页;
        /// </summary>
        private TabBase _selectedTab;
        public TabBase SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != value)
                {
                    Set(() => SelectedTab, ref _selectedTab, value);
                }
            }
        }

        /// <summary>
        /// 是否允许添加页签,当内存较小的时候，不允许添加基站，非基站页签除外;
        /// </summary>
        private bool _canAddTabs;
        public bool CanAddTabs
        {
            get => _canAddTabs;
            set
            {
                if (_canAddTabs == value) return;
                Set(() => CanAddTabs, ref _canAddTabs, value);
                AddTabCommand.RaiseCanExecuteChanged();
            }
        }

        private string _title = "DTMobile Station Combine Maintain Tool";
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                Set(()=> _title, ref _title, value);
            }
        }


        public MainWindowVM()
        {
            ItemCollection = new ObservableCollection<TabBase>();
            ItemCollection.CollectionChanged += ItemCollection_CollectionChanged;
            ReorderTabsCommand = new RelayCommand<TabReorder>(ReorderTabsCommandAction);
            AddTabCommand = new RelayCommand(AddTabCommandAction, () => CanAddTabs);
            CloseTabCommand = new RelayCommand<TabBase>(CloseTabCommandAction);
            CanAddTabs = true;

            // 当软件启动时，默认开启一个基站管理页的页签;
            NodeBListManagerTabVM ManagerListTab = new NodeBListManagerTabVM();
            ManagerListTab.TabName = "基站管理";
            ManagerListTab.onConnectNodeBEvt += ManagerListTab_AddMainWindowTags;
            ItemCollection.Add(ManagerListTab);

        }

        /// <summary>
        /// 当用户点击基站图标后，新建一个基站页签;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ManagerListTab_AddMainWindowTags(object sender, AddNodeBEvtArgs e)
        {
            ItemCollection.Add(e.para);
        }

        /// <summary>
        /// Reorder the tabs and refresh collection sorting.
        /// </summary>
        /// <param name="reorder"></param>
        protected virtual void ReorderTabsCommandAction(TabReorder reorder)
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(ItemCollection);
            int from = reorder.FromIndex;
            int to = reorder.ToIndex;
            var tabCollection = view.Cast<TabBase>().ToList();//Get the ordered collection of our tab control

            tabCollection[from].TabNumber = tabCollection[to].TabNumber; //Set the new index of our dragged tab

            if (to > from)
            {
                for (int i = from + 1; i <= to; i++)
                {
                    tabCollection[i].TabNumber--; //When we increment the tab index, we need to decrement all other tabs.
                }
            }
            else if (from > to)//when we decrement the tab index
            {
                for (int i = to; i < from; i++)
                {
                    tabCollection[i].TabNumber++;//When we decrement the tab index, we need to increment all other tabs.
                }
            }

            view.Refresh();//Refresh the view to force the sort description to do its work.
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void ItemCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (TabBase tab in e.NewItems)
                {
                    if (ItemCollection.Count > 1)
                    {
                        //If the new tab don't have an existing number, we increment one to add it to the end.
                        if (tab.TabNumber == 0)
                            tab.TabNumber = ItemCollection.OrderBy(x => x.TabNumber).LastOrDefault().TabNumber + 1;
                    }
                }
            }
            else
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(ItemCollection);
                view.Refresh();
                var tabCollection = view.Cast<TabBase>().ToList();
                foreach (var item in tabCollection)
                    item.TabNumber = tabCollection.IndexOf(item);
            }
        }

        /// <summary>
        /// 关闭标签页的时候执行的命令;
        /// </summary>
        /// <param name="vm"></param>
        private void CloseTabCommandAction(TabBase vm)
        {
            Console.WriteLine("Close Tab!");
            ItemCollection.Remove(vm);
        }

        /// <summary>
        /// 添加标签页的时候执行的命令;
        /// 默认为添加基站;
        /// </summary>
        private void AddTabCommandAction()
        {
            Console.WriteLine("Add tab by USER");
            ItemCollection.Add(CreateENodeBTab());
        }

        /// <summary>
        /// 添加一个基站页签;
        /// </summary>
        /// <returns></returns>
        protected TabBase CreateENodeBTab()
        {
            NodeBListManagerTabVM ManagerListTab = new NodeBListManagerTabVM();
            return ManagerListTab;
        }
    }
}
