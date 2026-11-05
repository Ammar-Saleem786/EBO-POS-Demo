using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Threading;
using FlexButton;
using Samba.Domain.Models.Tickets;
using Samba.Modules.PaymentModule;
using Samba.Presentation.Common.Commands;

class PaymentLayoutTests
{
    static void Assert(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS: " + name); }
    static List<T> Find<T>(DependencyObject root) where T:DependencyObject
    {
        var result=new List<T>();
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is T) result.Add((T)child);
            result.AddRange(Find<T>(child));
        }
        return result;
    }
    [STAThread] static int Main()
    {
        var app=new Application();
        Assembly.LoadFrom("Samba.Presentation.exe");
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/Samba.Presentation;component/Common.xaml",UriKind.Relative) });
        var vm=new PaymentButtonsViewModel(null);
        bool enabled=true; PaymentType selected=null;
        var command=new CaptionCommand<PaymentType>("Pay",p=>selected=p,p=>enabled);
        vm.SetButtonCommands(command,null,new CaptionCommand<string>("Close",p=>{},p=>true));
        var types=new List<PaymentType>();
        for(int i=0;i<12;i++) types.Add(new PaymentType { Name=i==0?"JazzCash":i==1?"Easypaisa":"Method "+i, FontSize=i==0?18:0, SortOrder=i, ButtonColor="Teal" });
        vm.PaymentButtonGroup.Update(types,null);
        var view=new PaymentButtonsView(vm);
        view.Measure(new Size(260,400));view.Arrange(new Rect(0,0,260,400));view.UpdateLayout();
        app.Dispatcher.Invoke(DispatcherPriority.DataBind,new Action(()=>{}));view.UpdateLayout();
        var buttons=Find<FlexButton.FlexButton>(view);
        Assert(buttons.Count==13,"12 configured methods and Close generated");
        Assert(buttons[0].Content.ToString()=="JazzCash" && buttons[1].Content.ToString()=="Easypaisa","new wallet captions displayed");
        Assert(object.ReferenceEquals(buttons[0].Command,command) && object.ReferenceEquals(buttons[0].CommandParameter,types[0]),"original command and payment type retained");
        Assert(buttons[0].FontSize==18 && buttons[1].FontSize==22,"configured font and readable default retained");
        var scroll=Find<ScrollViewer>(view)[0];
        Assert(scroll.ScrollableHeight>0,"overflow is scrollable");
        scroll.ScrollToBottom();view.UpdateLayout();
        Assert(scroll.VerticalOffset>0,"last methods and Close reachable by scrolling");
        buttons[1].Command.Execute(buttons[1].CommandParameter);
        Assert(object.ReferenceEquals(selected,types[1]),"Easypaisa button passes correct method");
        enabled=false;CommandManager.InvalidateRequerySuggested();
        app.Dispatcher.Invoke(DispatcherPriority.Background,new Action(()=>{}));
        Assert(!buttons[0].Command.CanExecute(buttons[0].CommandParameter),"original CanExecute restriction preserved");
        types.Add(new PaymentType {Name="Added after refresh",ButtonColor="Teal"});
        vm.PaymentButtonGroup.Update(types,null);
        app.Dispatcher.Invoke(DispatcherPriority.DataBind,new Action(()=>{}));view.UpdateLayout();
        Assert(Find<FlexButton.FlexButton>(view).Count==14,"new payment methods appear after collection refresh");
        return 0;
    }
}
