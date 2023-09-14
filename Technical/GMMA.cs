namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Guppy Multiple Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/49353-guppy-multiple-moving-average")]
	public class GMMA : Indicator
	{
		#region Fields

		private readonly EMA _emaLong1 = new();
		private readonly EMA _emaLong2 = new();
		private readonly EMA _emaLong3 = new();
		private readonly EMA _emaLong4 = new();
		private readonly EMA _emaLong5 = new();
		private readonly EMA _emaLong6 = new();

		private readonly EMA _emaShort1 = new();
		private readonly EMA _emaShort2 = new();
		private readonly EMA _emaShort3 = new();
		private readonly EMA _emaShort4 = new();
		private readonly EMA _emaShort5 = new();
		private readonly EMA _emaShort6 = new();

		private readonly ValueDataSeries _renderLong1 = new("RenderLong1", "Long1");
		private readonly ValueDataSeries _renderLong2 = new("RenderLong2", "Long2");
		private readonly ValueDataSeries _renderLong3 = new("RenderLong3", "Long3");
		private readonly ValueDataSeries _renderLong4 = new("RenderLong4", "Long4");
		private readonly ValueDataSeries _renderLong5 = new("RenderLong5", "Long5");
		private readonly ValueDataSeries _renderLong6 = new("RenderLong6", "Long6");

		private readonly ValueDataSeries _renderShort1 = new("RenderShort1", "Short1");
		private readonly ValueDataSeries _renderShort2 = new("RenderShort2", "Short2");
		private readonly ValueDataSeries _renderShort3 = new("RenderShort3", "Short3");
		private readonly ValueDataSeries _renderShort4 = new("RenderShort4", "Short4");
		private readonly ValueDataSeries _renderShort5 = new("RenderShort5", "Short5");
		private readonly ValueDataSeries _renderShort6 = new("RenderShort6", "Short6");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Colors), Order = 50)]
		public Color ShortColor
		{
			get => _renderShort1.Color;
			set =>
				_renderShort1.Color = _renderShort2.Color = _renderShort3.Color =
					_renderShort4.Color = _renderShort5.Color = _renderShort6.Color = value;
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Colors), Order = 60)]
		public Color LongColor
		{
			get => _renderLong1.Color;
			set =>
				_renderLong1.Color = _renderLong2.Color = _renderLong3.Color =
					_renderLong4.Color = _renderLong5.Color = _renderLong6.Color = value;
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod1), GroupName = nameof(Strings.ShortPeriod), Order = 100)]
		[Range(1, 10000)]
		public int EmaPeriod1
		{
			get => _emaShort1.Period;
			set
			{
				_emaShort1.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod2), GroupName = nameof(Strings.ShortPeriod), Order = 110)]
		[Range(1, 10000)]
        public int EmaPeriod2
		{
			get => _emaShort2.Period;
			set
			{
				_emaShort2.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod3), GroupName = nameof(Strings.ShortPeriod), Order = 120)]
		[Range(1, 10000)]
        public int EmaPeriod3
		{
			get => _emaShort3.Period;
			set
			{
				_emaShort3.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod4), GroupName = nameof(Strings.ShortPeriod), Order = 130)]
		[Range(1, 10000)]
        public int EmaPeriod4
		{
			get => _emaShort4.Period;
			set
			{
				_emaShort4.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod5), GroupName = nameof(Strings.ShortPeriod), Order = 140)]
		[Range(1, 10000)]
        public int EmaPeriod5
		{
			get => _emaShort5.Period;
			set
			{
				_emaShort5.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod6), GroupName = nameof(Strings.ShortPeriod), Order = 150)]
		[Range(1, 10000)]
		public int EmaPeriod6
		{
			get => _emaShort6.Period;
			set
			{
				_emaShort6.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod1), GroupName = nameof(Strings.LongPeriod), Order = 200)]
		[Range(1, 10000)]
        public int EmaLongPeriod1
		{
			get => _emaLong1.Period;
			set
			{
				_emaLong1.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod2), GroupName = nameof(Strings.LongPeriod), Order = 210)]
		[Range(1, 10000)]
        public int EmaLongPeriod2
		{
			get => _emaLong2.Period;
			set
			{
				_emaLong2.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod3), GroupName = nameof(Strings.LongPeriod), Order = 220)]
		[Range(1, 10000)]
        public int EmaLongPeriod3
		{
			get => _emaLong3.Period;
			set
			{
				_emaLong3.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod4), GroupName = nameof(Strings.LongPeriod), Order = 230)]
		[Range(1, 10000)]
        public int EmaLongPeriod4
		{
			get => _emaLong4.Period;
			set
			{
				_emaLong4.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod5), GroupName = nameof(Strings.LongPeriod), Order = 240)]
		[Range(1, 10000)]
        public int EmaLongPeriod5
		{
			get => _emaLong5.Period;
			set
			{
				_emaLong5.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EmaPeriod6), GroupName = nameof(Strings.LongPeriod), Order = 250)]
		[Range(1, 10000)]
        public int EmaLongPeriod6
		{
			get => _emaLong6.Period;
			set
			{
				_emaLong6.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public GMMA()
		{
			DenyToChangePanel = true;

			LongColor = DefaultColors.Red.Convert();
			ShortColor = DefaultColors.Blue.Convert();

			_emaShort1.Period = 3;
			_emaShort2.Period = 5;
			_emaShort3.Period = 7;
			_emaShort4.Period = 10;
			_emaShort5.Period = 12;
			_emaShort6.Period = 15;

			_emaLong1.Period = 30;
			_emaLong2.Period = 35;
			_emaLong3.Period = 40;
			_emaLong4.Period = 45;
			_emaLong5.Period = 50;
			_emaLong6.Period = 60;

			DataSeries[0] = _renderShort1;
			DataSeries.Add(_renderShort2);
			DataSeries.Add(_renderShort3);
			DataSeries.Add(_renderShort4);
			DataSeries.Add(_renderShort5);
			DataSeries.Add(_renderShort6);

			DataSeries.Add(_renderLong1);
			DataSeries.Add(_renderLong2);
			DataSeries.Add(_renderLong3);
			DataSeries.Add(_renderLong4);
			DataSeries.Add(_renderLong5);
			DataSeries.Add(_renderLong6);

			DataSeries.ForEach(x => x.IsHidden = true);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderShort1[bar] = _emaShort1.Calculate(bar, value);
			_renderShort2[bar] = _emaShort2.Calculate(bar, value);
			_renderShort3[bar] = _emaShort3.Calculate(bar, value);
			_renderShort4[bar] = _emaShort4.Calculate(bar, value);
			_renderShort5[bar] = _emaShort5.Calculate(bar, value);
			_renderShort6[bar] = _emaShort6.Calculate(bar, value);

			_renderLong1[bar] = _emaLong1.Calculate(bar, value);
			_renderLong2[bar] = _emaLong2.Calculate(bar, value);
			_renderLong3[bar] = _emaLong3.Calculate(bar, value);
			_renderLong4[bar] = _emaLong4.Calculate(bar, value);
			_renderLong5[bar] = _emaLong5.Calculate(bar, value);
			_renderLong6[bar] = _emaLong6.Calculate(bar, value);
		}

		#endregion
	}
}