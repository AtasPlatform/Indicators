namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Moving Average Difference")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45297-moving-average-difference")]
	public class MaDifference : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

		private readonly SMA _sma1 = new() { Period = 10 };
		private readonly SMA _sma2 = new() { Period = 20 };

		private System.Drawing.Color _negColor = DefaultColors.Red;
		private System.Drawing.Color _posColor = DefaultColors.Green;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Up", GroupName = "Drawing", Order = 610)]
        public System.Windows.Media.Color PosColor
        {
	        get => _posColor.Convert();
	        set
	        {
		        _posColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Down", GroupName = "Drawing", Order = 620)]
        public System.Windows.Media.Color NegColor
        {
	        get => _negColor.Convert();
	        set
	        {
		        _negColor = value.Convert();
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "SMA1", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int Period1
		{
			get => _sma1.Period;
			set
			{
				_sma1.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMA2", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int Period2
		{
			get => _sma2.Period;
			set
			{
				_sma2.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MaDifference()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma1.Calculate(bar, value);
			_sma2.Calculate(bar, value);

			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			var diff = _sma1[bar] - _sma2[bar];
			_renderSeries[bar] = diff;
			_renderSeries.Colors[bar] = diff > _renderSeries[bar - 1] ? _posColor : _negColor;
		}

		#endregion
	}
}