namespace ATAS.Indicators.Technical
{
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Editors;

	using OFT.Attributes;
	using OFT.Attributes.Editors;
	using OFT.Rendering.Settings;

	[DisplayName("Properties")]
	[Category("Samples")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/SampleProperties")]
	public class SampleProperties : Indicator
	{
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

		public class Range : NotifyPropertyChangedBase
		{
			#region Private fields

			private int _from;
			private int _to;

			#endregion

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
		}

		#endregion

		#region Properties

		[Display(Name = "Font", GroupName = "Examples")]
		public FontSetting Font { get; set; } = new FontSetting();

		[Display(Name = "Pen", GroupName = "Examples")]
		public PenSettings Pen { get; set; } = new PenSettings{Color = Colors.Red, Width = 1};

		[Display(Name = "Brush", GroupName = "Examples")]
		public BrushSettings Brush { get; set; } = new BrushSettings{ StartColor = Colors.Red, EndColor = Colors.Yellow, UseEndColor = true};

		[Display(Name = "Filter enum", GroupName = "Examples")]
		public Filter<FilterTypes> FilterType { get; set; } = new Filter<FilterTypes> { Enabled = true };

		[Display(Name = "Filter decimal", GroupName = "Examples")]
		[Range(-100, 100)]
		[DisplayFormat(DataFormatString = "##0.0##")]
		public Filter FilterDecimal { get; set; } = new Filter();

		[Display(Name = "Filter integer", GroupName = "Examples")]
		public FilterInt FilterInt { get; set; } = new FilterInt();

		[Display(Name = "Filter text", GroupName = "Examples")]
		[Mask(MaskTypes.Regular, "..-..")]
		public FilterString FilterText { get; set; } = new FilterString();

		[Display(Name = "Track bar", GroupName = "Examples")]
		[NumericEditor(NumericEditorTypes.TrackBar, -100, 100)]
		public int TrackBar { get; set; }

		[Display(Name = "Enum", GroupName = "Examples")]
		public FilterTypes Enum { get; set; }

		[Display(Name = "Selector", GroupName = "Examples")]
		[ComboBoxEditor(nameof(GetEntities), DisplayMember = nameof(Entity.Name), ValueMember = nameof(Entity.Value))]
		public int? Selector { get; set; }

		[IsExpanded]
		[Display(Name = "Numbers", GroupName = "Examples")]
		[Range(0, 100)]
		[DisplayFormat(DataFormatString = "P")]
		public ObservableCollection<decimal> Numbers { get; set; } = new ObservableCollection<decimal>();

		[Display(Name = "Filters", GroupName = "Examples")]
		[DisplayFormat(DataFormatString = "F2")]
		public ObservableCollection<Filter> Filters { get; set; } = new ObservableCollection<Filter>();

		[Display(Name = "Colors", GroupName = "Examples")]
		public ObservableCollection<Color> ColorsSource { get; set; } = new ObservableCollection<Color>();

		[Display(Name = "Decimal", GroupName = "Examples")]
		[DisplayFormat(DataFormatString = "F3")]
		public decimal Decimal { get; set; }

		[Display(Name = "Integer", GroupName = "Examples")]
		[Range(0, 100)]
		public int Integer { get; set; }

		[Display(Name = "Range", GroupName = "Examples")]
		[Editor(typeof(RangeEditor), typeof(RangeEditor))]
		public Range FilterRange { get; set; } = new Range{From = 0, To = 10};

		#endregion

		#region Private methods

		private Entity[] GetEntities()
		{
			return new[]
			{
				new Entity{Value = 1, Name = "Entity 1"},
				new Entity{Value = 2, Name = "Entity 2"},
				new Entity{Value = 3, Name = "Entity 3"},
				new Entity{Value = 4, Name = "Entity 4"},
				new Entity{Value = 5, Name = "Entity 5"}
			};
		}

		#endregion

		#region Overrides of BaseIndicator

		protected override void OnCalculate(int bar, decimal value)
		{
			
		}

		#endregion
	}
}