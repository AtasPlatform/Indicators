namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("Ichimoku Kinko Hyo")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/16981-ichimoku-kinko-hyo")]
	public class Ichimoku : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _chikouSeries = new("Chikou Span");
		private readonly Highest _highestKijun = new();
		private readonly Highest _highestSenkou = new();

		private readonly Highest _highestTenkan = new();
		private readonly ValueDataSeries _kijunSeries = new("Kijun-sen");
		private readonly ValueDataSeries _kumoDownSeries = new("Down Kumo");
		private readonly ValueDataSeries _kumoUpSeries = new("Up Kumo");
		private readonly Lowest _lowestKijun = new();
		private readonly Lowest _lowestSenkou = new();
		private readonly Lowest _lowestTenkan = new();
		private readonly RangeDataSeries _senkouSpanBand = new("Senkou Span");
		private readonly ValueDataSeries _tenkanSeries = new("Tenkan-sen");
		private int _days;
		private int _extBegin;
		private int _kijun;
		private int _senkou;
		private int _targetBar;
		private int _tenkan;

		#endregion

		#region Properties

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Days")]
		public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Tenkan-sen")]
		public int Tenkan
		{
			get => _tenkan;
			set
			{
				if (value <= 0)
					return;

				_tenkan = _highestTenkan.Period = _lowestTenkan.Period = value;
				_extBegin = _kijun;

				if (_extBegin < _tenkan)
					_extBegin = _tenkan;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Kijun-sen")]
		public int Kijun
		{
			get => _kijun;
			set
			{
				if (value <= 0)
					return;

				_kijun = _highestKijun.Period = _lowestKijun.Period = value;
				_extBegin = _kijun;

				if (_extBegin < _tenkan)
					_extBegin = _tenkan;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Senkou Span B")]
		public int Senkou
		{
			get => _senkou;
			set
			{
				if (value <= 0)
					return;

				_senkou = _highestSenkou.Period = _lowestSenkou.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Ichimoku()
			: base(true)
		{
			DenyToChangePanel = true;
			_tenkan = _highestTenkan.Period = _lowestTenkan.Period = 9;
			_kijun = _highestKijun.Period = _lowestKijun.Period = 26;
			_senkou = _highestSenkou.Period = _lowestSenkou.Period = 52;
			_extBegin = _kijun;
			_days = 20;

			if (_extBegin < _tenkan)
				_extBegin = _tenkan;

			_kijunSeries.Color = Colors.Blue;
			_chikouSeries.Color = Colors.Lime;
			_kumoUpSeries.Color = Colors.SandyBrown;
			_kumoDownSeries.Color = Colors.Thistle;

			DataSeries[0] = _tenkanSeries;
			DataSeries.Add(_kijunSeries);
			DataSeries.Add(_chikouSeries);
			DataSeries.Add(_kumoUpSeries);
			DataSeries.Add(_kumoDownSeries);
			DataSeries.Add(_senkouSpanBand);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_targetBar = 0;

				if (_days > 0)
				{
					var days = 0;

					for (var i = CurrentBar - 1; i >= 0; i--)
					{
						_targetBar = i;

						if (!IsNewSession(i))
							continue;

						days++;

						if (days == _days)
							break;
					}

					if (_targetBar > 0)
					{
						_tenkanSeries.SetPointOfEndLine(_targetBar - 1);
						_kijunSeries.SetPointOfEndLine(_targetBar - 1);
						_chikouSeries.SetPointOfEndLine(Math.Max(0, _targetBar - _kijun - 1));
					}
				}
			}

			if (bar < _targetBar)
				return;

			var candle = GetCandle(bar);

			_highestKijun.Calculate(bar, candle.High);
			_highestTenkan.Calculate(bar, candle.High);
			_highestSenkou.Calculate(bar, candle.High);
			_lowestKijun.Calculate(bar, candle.Low);
			_lowestTenkan.Calculate(bar, candle.Low);
			_lowestSenkou.Calculate(bar, candle.Low);

			_tenkanSeries[bar] = (_highestTenkan[bar] + _lowestTenkan[bar]) / 2;
			_kijunSeries[bar] = (_highestKijun[bar] + _lowestKijun[bar]) / 2;

			if (bar + _kijun <= CurrentBar - 1)
			{
				_senkouSpanBand[bar + _kijun].Upper = Math.Min(_tenkanSeries[bar], _kijunSeries[bar]) + (_tenkanSeries[bar] - _kijunSeries[bar]) / 2;
				_senkouSpanBand[bar + _kijun].Lower = (_highestSenkou[bar] + _lowestSenkou[bar]) / 2;
				_kumoUpSeries[bar + _kijun] = _senkouSpanBand[bar + _kijun].Upper;
				_kumoDownSeries[bar + _kijun] = _senkouSpanBand[bar + _kijun].Lower;
			}

			if (bar < _kijun)
				return;

			_chikouSeries[bar - _kijun] = candle.Close;

			for (var i = bar - Kijun + 1; i < CurrentBar; i++)
				_chikouSeries[i] = candle.Close;
		}

		#endregion
	}
}