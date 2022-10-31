namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Williams' %R")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45251-williams-r")]
	public class WilliamsR : Indicator
	{
		#region Fields

		private readonly Highest _highest = new() { Period = 10 };
		private readonly Lowest _lowest = new() { Period = 10 };
		
		private bool _invertOutput;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "InvertOutput", GroupName = "Settings", Order = 110)]
		public bool InvertOutput
		{
			get => _invertOutput;
			set
			{
				_invertOutput = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WilliamsR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_highest.Period = _lowest.Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highest.Calculate(bar, candle.High);
			_lowest.Calculate(bar, candle.Low);

			var renderValue = _highest[bar] != _lowest[bar]
				? 100 * (_highest[bar] - candle.Close) / (_highest[bar] - _lowest[bar])
				: 0m;
			
			this[bar] = _invertOutput
				? -renderValue
				: renderValue;
		}

		#endregion
	}
}