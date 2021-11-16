namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Arms Ease of Movement")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43349-arms-ease-of-movement")]
	public class EMV : Indicator
	{
		#region Nested types

		public enum MovingType
		{
			[Display(ResourceType = typeof(Resources), Name = "EMA")]
			Ema,

			[Display(ResourceType = typeof(Resources), Name = "LinearReg")]
			LinReg,

			[Display(ResourceType = typeof(Resources), Name = "WMA")]
			Wma,

			[Display(ResourceType = typeof(Resources), Name = "SMA")]
			Sma,

			[Display(ResourceType = typeof(Resources), Name = "SMMA")]
			Smma
		}

		#endregion

		#region Fields

		private readonly EMA _emaRender = new();

		private readonly ValueDataSeries _renderSeries = new("ADXR");

		private object _movingIndicator;
		private MovingType _movingType;

		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "MovingType", GroupName = "Settings", Order = 100)]
		public MovingType MaType
		{
			get => _movingType;
			set
			{
				_movingType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public EMV()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_period = 9;
			_emaRender.Period = 4;
			_movingType = MovingType.Ema;

			_renderSeries.Color = Colors.Red;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			switch (_movingType)
			{
				case MovingType.Ema:
					_movingIndicator = new EMA
						{ Period = _period };
					break;
				case MovingType.LinReg:
					_movingIndicator = new LinearReg
						{ Period = _period };
					break;
				case MovingType.Wma:
					_movingIndicator = new WMA
						{ Period = _period };
					break;
				case MovingType.Sma:
					_movingIndicator = new SMA
						{ Period = _period };
					break;
				case MovingType.Smma:
					_movingIndicator = new SMMA
						{ Period = _period };
					break;
			}
		}

		#endregion

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				IndicatorCalculate(bar, _movingType, 0);
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);
			var midPoint = (candle.High + candle.Low) / 2m - (prevCandle.High + prevCandle.Low) / 2m;
			var ratio = candle.High - candle.Low == 0 ? 0 : candle.Volume / (candle.High - candle.Low);
			var emv = ratio == 0 ? 0 : midPoint / ratio;
			_renderSeries[bar] = IndicatorCalculate(bar, _movingType, emv);
		}

		#endregion

		#region Private methods

		private decimal IndicatorCalculate(int bar, MovingType type, decimal value)
		{
			var movingValue = 0m;

			switch (type)
			{
				case MovingType.Ema:
					movingValue = ((EMA)_movingIndicator).Calculate(bar, value);
					break;
				case MovingType.LinReg:
					movingValue = ((LinearReg)_movingIndicator).Calculate(bar, value);
					break;
				case MovingType.Wma:
					movingValue = ((WMA)_movingIndicator).Calculate(bar, value);
					break;
				case MovingType.Sma:
					movingValue = ((SMA)_movingIndicator).Calculate(bar, value);
					break;
				case MovingType.Smma:
					movingValue = ((SMMA)_movingIndicator).Calculate(bar, value);
					break;
			}

			return movingValue;
		}

		#endregion
	}
}