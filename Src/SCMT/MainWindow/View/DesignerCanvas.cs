﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Xml;

namespace SCMTMainWindow.View
{
    public partial class DesignerCanvas : Canvas
    {
        private Point? rubberbandSelectionStartPoint = null;

        private SelectionService selectionService;
        internal SelectionService SelectionService
        {
            get
            {
                if (selectionService == null)
                    selectionService = new SelectionService(this);

                return selectionService;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Source == this)
            {
                // in case that this click is the start of a 
                // drag operation we cache the start point
                this.rubberbandSelectionStartPoint = new Point?(e.GetPosition(this));

                // if you click directly on the canvas all 
                // selected items are 'de-selected'
                SelectionService.ClearSelection();
                Focus();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // if mouse button is not pressed we have no drag operation, ...
            if (e.LeftButton != MouseButtonState.Pressed)
                this.rubberbandSelectionStartPoint = null;

            // ... but if mouse button is pressed and start
            // point value is set we do have one
            if (this.rubberbandSelectionStartPoint.HasValue)
            {
                // create rubberband adorner
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if (adornerLayer != null)
                {
                    RubberbandAdorner adorner = new RubberbandAdorner(this, rubberbandSelectionStartPoint);
                    if (adorner != null)
                    {
                        adornerLayer.Add(adorner);
                    }
                }
            }
            e.Handled = true;
        }


        private string GetElementFromXAML(string strRRU, string strXAML)
        {
            Uri strUri = new Uri("pack://application:,,,/View/Resources/Stencils/XMLFile1.xml");
            Stream stream = Application.GetResourceStream(strUri).Stream;

            FrameworkElement el = XamlReader.Load(stream) as FrameworkElement;

            string strName = string.Empty;
            if(strRRU == "单通道")
            {
                strName = "g_OnePathRRU";
            }else if(strRRU == "双通道")
            {
                strName = "g_TwoPathRRU";
            }
            else if (strRRU == "四通道")
            {
                strName = "g_FourPathRRU";
            }

            Object content = el.FindName(strName) as Grid;

            strXAML = XamlWriter.Save(content);

            return strXAML;
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            DragObject dragObject = e.Data.GetData(typeof(DragObject)) as DragObject;

            //这样子便可以设置  元素的大小了
            //dragObject.DesiredSize = new Size(400, 180);

            if (dragObject != null && !String.IsNullOrEmpty(dragObject.Xaml))
            {
                DesignerItem newItem = null;
                Object content = XamlReader.Load(XmlReader.Create(new StringReader(dragObject.Xaml)));

                if (content != null)
                {
                    ChooseRRUType dlgChooseRRU = new ChooseRRUType();
                    dlgChooseRRU.ShowDialog();

                    if (!dlgChooseRRU.bOK)
                    {
                        return;    //选择取消之后，不进行拖拽
                    }
                    string strRRUtype = dlgChooseRRU.strRRUtype;
                    //MessageBox.Show(strRRUtype);
                    string strXAML = string.Empty;
                    strXAML =  GetElementFromXAML(strRRUtype, strXAML);

                    Object testContent = XamlReader.Load(XmlReader.Create(new StringReader(strXAML)));

                    newItem = new DesignerItem();
                    newItem.Content = testContent;

                    Point position = e.GetPosition(this);

                    if (dragObject.DesiredSize.HasValue)
                    {
                        Size desiredSize = dragObject.DesiredSize.Value;
                        newItem.Width = desiredSize.Width;
                        newItem.Height = desiredSize.Height;

                        DesignerCanvas.SetLeft(newItem, Math.Max(0, position.X - newItem.Width / 2));
                        DesignerCanvas.SetTop(newItem, Math.Max(0, position.Y - newItem.Height / 2));
                    }
                    else
                    {
                        DesignerCanvas.SetLeft(newItem, Math.Max(0, position.X));
                        DesignerCanvas.SetTop(newItem, Math.Max(0, position.Y));
                    }

                    Canvas.SetZIndex(newItem, this.Children.Count);
                    this.Children.Add(newItem);                    
                    SetConnectorDecoratorTemplate(newItem);

                    //update selection
                    this.SelectionService.SelectItem(newItem);
                    newItem.Focus();
                }

                e.Handled = true;
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Size size = new Size();

            foreach (UIElement element in this.InternalChildren)
            {
                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);
                left = double.IsNaN(left) ? 0 : left;
                top = double.IsNaN(top) ? 0 : top;

                //measure desired size for each child
                element.Measure(constraint);

                Size desiredSize = element.DesiredSize;
                if (!double.IsNaN(desiredSize.Width) && !double.IsNaN(desiredSize.Height))
                {
                    size.Width = Math.Max(size.Width, left + desiredSize.Width);
                    size.Height = Math.Max(size.Height, top + desiredSize.Height);
                }
            }
            // add margin 
            size.Width += 10;
            size.Height += 10;
            return size;
        }

        private void SetConnectorDecoratorTemplate(DesignerItem item)
        {
            if (item.ApplyTemplate() && item.Content is UIElement)
            {
                ControlTemplate template = DesignerItem.GetConnectorDecoratorTemplate(item.Content as UIElement);
                Control decorator = item.Template.FindName("PART_ConnectorDecorator", item) as Control;
                if (decorator != null && template != null)
                    decorator.Template = template;
            }
        }
    }
}