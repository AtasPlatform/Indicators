﻿namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Net;
	using System.Security;

	using ATAS.Indicators.Technical.Editors;

	using Newtonsoft.Json;

	using OFT.Attributes.Editors;
	using OFT.Rendering.Heatmap;
	using OFT.Rendering.Settings;

	using static System.Environment;

    [DisplayName("Properties")]
	[Category("Samples")]
	public class SampleProperties : Indicator, IPropertiesEditorOwner
	{
		#region Private fields

		private const string _managedCategoryCategoryName = "ManagedCategory";

		private bool              _activateProperties;
		private bool              _categoryManager;
		private IPropertiesEditor _propertiesEditor;
		private bool              _propertyManager;

		#endregion

		#region Properties

		[Display(Name = "Font", GroupName = "Examples")]
		public FontSetting Font { get; set; } = new();

		[Display(Name = "Pen", GroupName = "Examples")]
		public PenSettings Pen { get; set; } = new()
			{ Color = System.Drawing.Color.Red.Convert(), Width = 1 };

		[Display(Name = "Brush", GroupName = "Examples")]
		public BrushSettings Brush { get; set; } = new()
			{ StartColor = System.Drawing.Color.Red.Convert(), EndColor = System.Drawing.Color.Yellow.Convert(), UseEndColor = true };

		[Display(Name = "Heatmap", GroupName = "Examples")]
		public HeatmapTypes HeatmapType { get; set; }

		[Display(Name = "Filter Enum", GroupName = "Examples")]
		public Filter<FilterTypes> FilterType { get; set; } = new()
			{ Enabled = true };

		[Display(Name = "Filter Decimal", GroupName = "Examples")]
		[Range(-100, 100)]
		[DisplayFormat(DataFormatString = "##0.0##")]
		public Filter FilterDecimal { get; set; } = new();

		[Display(Name = "Filter Integer", GroupName = "Examples")]
		public FilterInt FilterInt { get; set; } = new();

		[Display(Name = "Filter Text", GroupName = "Examples")]
		[Mask(MaskTypes.Regular, "..-..")]
		public FilterString FilterText { get; set; } = new();

		[Display(Name = "Track Bar", GroupName = "Examples")]
		[NumericEditor(NumericEditorTypes.TrackBar, -100, 100)]
		public int TrackBar { get; set; }

		[Display(Name = "Enum", GroupName = "Examples")]
		public FilterTypes Enum { get; set; }

		[Display(Name = "Selector", GroupName = "Examples")]
		[ComboBoxEditor(typeof(EntitiesSource), DisplayMember = nameof(Entity.Name), ValueMember = nameof(Entity.Value))]
		public int? Selector { get; set; }

		[IsExpanded]
		[Display(Name = "Numbers", GroupName = "Examples")]
		[Range(0, 100)]
		[DisplayFormat(DataFormatString = "P")]
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<decimal> Numbers { get; set; } = new()
			{ 1.0m, 2.0m, 3.0m };

		[Display(Name = "Filters", GroupName = "Examples")]
		[DisplayFormat(DataFormatString = "F2")]
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<Filter> Filters { get; set; } = new();

		[IsExpanded]
		[Display(Name = "Colors", GroupName = "Examples")]
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<CrossColor> ColorsSource { get; set; } = new()
            {  System.Drawing.Color.Red.Convert(), System.Drawing.Color.Green.Convert(), System.Drawing.Color.Blue.Convert() };

		[Display(Name = "Ranges", GroupName = "Examples")]
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<Range> Ranges { get; set; } = new();

		[IsExpanded]
		[Display(Name = "Range", GroupName = "Examples")]
		public Range FilterRange { get; set; } = new()
			{ From = 0, To = 10 };

		[Display(Name = "Hot Key", GroupName = "Examples")]
		public CrossKey[] HotKeys { get; set; } = { CrossKey.V };

		[Display(Name = "Decimal", GroupName = "Examples")]
		[NumericEditor(0.0, 100.0, Step = 0.5, DisplayFormat = "F2")]
		public decimal Decimal { get; set; }

		[Display(Name = "Integer", GroupName = "Examples")]
		[Range(0, 100)]
		public int Integer { get; set; }

		[Display(Name = "Boolean", GroupName = "Examples")]
		public bool Boolean { get; set; }

		[Display(Name = "Time Span", GroupName = "Examples")]
		[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm:ss")]
		public TimeSpan TimeSpan { get; set; } = new(1, 0, 0);

		[Display(Name = "Date", GroupName = "Examples")]
		[Mask(MaskTypes.DateTime, "dd.MM.yyyy")]
		public DateTime DateTime { get; set; } = new(2020, 01, 01);

		[Display(Name = "Time Zone", GroupName = "Examples")]
		public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

		[Display(Name = "Password", GroupName = "Examples")]
		public SecureString Password { get; set; }

		[Display(Name = "IP-address", GroupName = "Examples")]
		public EndPoint IpAddress { get; set; }

		[Display(Name = "E-mail", GroupName = "Examples")]
		[RegularExpression(@"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$")]
		public string Email { get; set; }

		[Display(Name = "File", GroupName = "Examples")]
		[SelectFileEditor(SpecialFolder.MyDocuments, Filter = "Text files (*.txt)|*.txt", IsTextEditable = false)]
		public string File { get; set; }

		[Display(Name = "Files", GroupName = "Examples")]
		[SelectFileEditor(SpecialFolder.MyDocuments, Filter = "Text files (*.txt)|*.txt", IsTextEditable = false, Multiselect = true)]
		public string Files { get; set; }

		[Display(Name = "Directory", GroupName = "Examples")]
		[SelectDirectoryEditor(IsTextEditable = false, ShowNewFolderButton = true)]
		public string Directory { get; set; }

		[Browsable(false)]
		public int NoBrowsable { get; set; }

		[Display(Name = "Enable Properties", GroupName = "Activate properties")]
		public bool ActivateProperties
		{
			get => _activateProperties;
			set
			{
				_activateProperties = value;
				ActiveFilter1.Enabled = ActiveFilter2.Enabled = ActiveFilter3.Enabled = value;
			}
		}

		[Display(Name = "Number property", GroupName = "Activate properties")]
		public Filter ActiveFilter1 { get; set; } = new(false) { Value = 123.456m };

		[Display(Name = "Bool property", GroupName = "Activate properties")]
		public FilterBool ActiveFilter2 { get; set; } = new(false);

		[Display(Name = "String property", GroupName = "Activate properties")]
		public FilterString ActiveFilter3 { get; set; } = new(false) { Value = "1234abcd" };

		[IsExpanded]
		[Display(Name = "Custom class", GroupName = "Custom class properties")]
		public CustomClass CustomProperty { get; set; } = new();
		
		[Display(Name = "Category", GroupName = "Management")]
		public bool CategoryManager
		{
			get => _categoryManager;
			set => SetProperty(ref _categoryManager, value, () => _propertiesEditor?.SetIsExpandedCategory(_managedCategoryCategoryName, value));
		}

		[Display(Name = "Property", GroupName = "Management")]
		public bool PropertyManager
		{
			get => _propertyManager;
			set => SetProperty(ref _propertyManager, value, () => _propertiesEditor?.SetIsExpandedProperty(nameof(CustomProperty), value));
		}

		[Display(Name = "Managed Property 1", GroupName = _managedCategoryCategoryName)]
		public int ManagedProperty1 { get; set; }

		[Display(Name = "Managed Property 2", GroupName = _managedCategoryCategoryName)]
		public int ManagetProperty2 { get; set; }

		#endregion

        #region ctor

        public SampleProperties()
			: base(true)
		{
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			Panel = IndicatorDataProvider.NewPanel;
		}

		#endregion

		#region Protected methods

		#region Overrides of BaseIndicator

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		#endregion

		#endregion

		#region Nested classes

		public enum FilterTypes
		{
			[Display(Name = "Type 1")]
			Type1,

			[Display(Name = "Type 2")]
			Type2,

			[Display(Name = "Type 3")]
			Type3
		}

		public class Entity
		{
			#region Properties

			public int Value { get; set; }

			public string Name { get; set; }

			#endregion
		}


        [Editor(typeof(RangeEditor), typeof(RangeEditor))]
        public class Range : NotifyPropertyChangedBase
		{
			#region Properties

			public int From
			{
				get => _from;
				set => SetProperty(ref _from, value);
			}

			public int To
			{
				get => _to;
				set => SetProperty(ref _to, value);
			}

			#endregion

			#region Private fields

			private int _from;
			private int _to;

			#endregion
		}

		private class EntitiesSource : Collection<Entity>
		{
			#region ctor

			public EntitiesSource()
				: base(new[]
				{
					new Entity { Value = 1, Name = "Entity 1" },
					new Entity { Value = 2, Name = "Entity 2" },
					new Entity { Value = 3, Name = "Entity 3" },
					new Entity { Value = 4, Name = "Entity 4" },
					new Entity { Value = 5, Name = "Entity 5" }
				})
			{
			}

			#endregion
		}

		#endregion

		#region Implementation of IPropertiesEditorOwner

		[Browsable(false)]
		IPropertiesEditor IPropertiesEditorOwner.PropertiesEditor
		{
			get => _propertiesEditor;
			set
			{
				if(_propertiesEditor == value) 
					return;

				var oldValue = _propertiesEditor;
				_propertiesEditor = value;

				PropertiesEditorOnChanged(oldValue, value);
			}
		}

		private void PropertiesEditorOnChanged(IPropertiesEditor oldValue, IPropertiesEditor newValue)
		{
			//TODO: Add code when show in editor. Parameters oldValue or newValue may be null.

			_categoryManager = newValue?.GetIsExpandedCategory(_managedCategoryCategoryName) ?? false;
			_propertyManager = newValue?.GetIsExpandedProperty(nameof(CustomProperty)) ?? false;
		}

		#endregion
	}
	
    [Editor(typeof(SampleEditor), typeof(SampleEditor))]
    public class CustomClass
    {
	    [Display(Name = "Enum property")]
	    public PictureChoiceSample EnumProperty { get; set; }

	    [Display(Name = "Number property")]
		public decimal Number { get; set; } = 123.456m;

        [Display(Name = "String property")]
		public string Str { get; set; } = "abcd";

        [Display(Name = "Hotkey property")]
		public CrossKey[] Keys { get; set; } = { CrossKey.F };
		
		[Display(Name = "Font property")]
		public FontSetting Font { get; set; } = new();

		[Display(Name = "Color property")]
		public CrossColor ColorProperty { get; set; } = System.Drawing.Color.Aqua.Convert();
    }

	public enum PictureChoiceSample
	{
		[Display(Name = "Picture 1")]
		Picture1,

		[Display(Name = "Picture 2")]
		Picture2
    }
}