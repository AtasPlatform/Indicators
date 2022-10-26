namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Force Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45197-force-indexforce-index-average")]
	public class ForceIndex : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private bool _useEma;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "UseMA", GroupName = "Settings", Order = 100)]
		public bool UseEma
		{
			get => _useEma;
			set
			{
				_useEma = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ForceIndex()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_ema.Calculate(bar, 0);

			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var force = candle.Volume * (candle.Close - prevCandle.Close);
			
			_renderSeries[bar] = _useEma 
				? _ema.Calculate(bar, force)
				: force;
		}

		#endregion
	}
}