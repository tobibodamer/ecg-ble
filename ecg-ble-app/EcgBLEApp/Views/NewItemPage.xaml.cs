using EcgBLEApp.Models;
using EcgBLEApp.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui; 
using Microsoft.Maui.Controls.Xaml;

namespace EcgBLEApp.Views
{
    public partial class NewItemPage : ContentPage
    {
        public Item Item { get; set; }

        public NewItemPage()
        {
            InitializeComponent();
            BindingContext = new NewItemViewModel();
        }
    }
}