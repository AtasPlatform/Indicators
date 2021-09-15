namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Bid Ask Volume Ratio")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/43420-bid-ask-volume-ratio")]
	public class BidAskVR : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = "AskBid")]
			AskBid,

			[Display(ResourceType = typeof(Strings), Name = "BidAsk")]
			BidAsk
		}

		public enum MovingType
		{
			[Display(ResourceType = typeof(Strings), Name = "EMA")]
			Ema,

			[Display(ResourceType = typeof(Strings), Name = "LinearReg")]
			LinReg,

			[Display(ResourceType = typeof(Strings), Name = "WMA")]
			Wma,

			[Display(ResourceType = typeof(Strings), Name = "SMA")]
			Sma,

			[Display(ResourceType = typeof(Strings), Name = "SMMA")]
			Smma
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _vr = new("VR");
		private readonly ValueDataSeries _vrMa = new(Strings.Visualization);
		private Mode _calcMode;

		private object _movingIndicator;
		private MovingType _movingType;
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "MovingType", GroupName = "Settings", Order = 100)]
		public MovingType MaType
		{
			get => _movingType;
			set
			{
				_movingType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "Period", GroupName = "Settings", Order = 110)]
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

		[Display(ResourceType = typeof(Strings), Name = "Mode", GroupName = "Settings", Order = 120)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BidAskVR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 10;
			_vrMa.VisualType = VisualMode.Histogram;
			DataSeries[0] = _vrMa;
		}

		#endregion

		#region Protected methods

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

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var diff = _calcMode == Mode.AskBid
				? candle.Ask - candle.Bid
				: candle.Bid - candle.Ask;

			_vr[bar] = 100 * diff / (candle.Ask + candle.Bid);

			if (bar < _period && bar > 0)
			{
				if (_vrMa[bar - 1] == 0)
					_vrMa[bar] = 2m / (bar + 2) * _vr[bar] + (1 - 2m / (bar + 2)) * _vr[bar - 1];
				else
					_vrMa[bar] = 2m / (bar + 2) * _vr[bar] + (1 - 2m / (bar + 2)) * _vrMa[bar - 1];
			}

			if (bar >= _period)
				_vrMa[bar] = IndicatorCalculate(bar, _movingType, _vr[bar]);
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