using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using ReactiveUI;

namespace PurpleExplorer.Models
{
    public class ServiceBusResource 
    {
        public string Name;
        public ObservableCollection<ServiceBusTopic> Topics { get; set; }
    }
}