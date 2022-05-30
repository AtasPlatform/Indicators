namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;
	
	public class OptionNiked : Indicator
	{
		#region Fields

		private ValueDataSeries _bnd = new("bnd");
		private ValueDataSeries _bnu = new("bnu");
		private ValueDataSeries _closeSeries = new("Close");

		private RangeDataSeries _cloud = new(Resources.Range);

		private int _days;
		private ValueDataSeries _dopnd = new("dopnd");
		private ValueDataSeries _dopnu = new("dopnu");

		private ValueDataSeries _downArrows = new(Resources.Down)
		{
			VisualType = VisualMode.DownArrow
		};

		private ValueDataSeries _ffSeries = new("ff series");
		private int _gran = 15;
		private ValueDataSeries _grH = new("grh");
		private ValueDataSeries _grL = new("grl");
		private ValueDataSeries _hh = new("hh");
		private Highest _highestClose = new();
		private Highest _highestOtn = new();
		private ValueDataSeries _inertiaFf = new("inertia ff");
		private decimal _koef = 100;
		private LinearReg _linRegClose = new();
		private LinRegSlope _linRegSlope = new();
		private LinRegSlope _linRegSlopeClose = new();
		private ValueDataSeries _ll = new("ll");

		private ValueDataSeries _longValues = new("long values");
		private Lowest _lowestClose = new();
		private Lowest _lowestOtn = new();
		private ValueDataSeries _nd = new("nd");
		private ValueDataSeries _nu = new("nu");
		private int _period = 13;
		private int _period3 = 130;
		private int _targetBar;

		private ValueDataSeries _upArrows = new(Resources.Up)
		{
			Color = Colors.Green,
			VisualType = VisualMode.UpArrow
		};

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Days", GroupName = "Common", Order = 115)]
		[Range(0, 1000)]
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 115)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				_linRegSlope.Period = value;
				_linRegClose.Period = value;
				_highestClose.Period = _lowestClose.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 120)]
		[Range(1, 10000)]
		public int Period3
		{
			get => _period3;
			set
			{
				_period3 = value;
				_highestOtn.Period = _lowestOtn.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public OptionNiked()
			: base(true)
		{
			_cloud.RangeColor = Color.FromArgb(128, 0, 0, 255);
			DataSeries[0] = _upArrows;
			DataSeries.Add(_downArrows);
			DataSeries.Add(_cloud);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_closeSeries[bar] = candle.Close;
			var maxs = _highestClose.Calculate(bar, candle.Close);
			var mins = _lowestClose.Calculate(bar, candle.Close);

			if (bar == 0)
			{
				_ffSeries.Clear();
				_inertiaFf.Clear();
				_longValues.Clear();
				_grH.Clear();
				_grL.Clear();
				_dopnu.Clear();
				_nu.Clear();
				_dopnd.Clear();
				_nd.Clear();

				DataSeries.ForEach(x => x.Clear());
				_nu[Period - 1] = _nd[Period - 1] = 1;
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
						_nu[_targetBar + Period - 1] = _nd[_targetBar + Period - 1] = 1;
				}

				return;
			}

			if (bar < _targetBar + Period)
				return;

			var ff = candle.Close;
			_ffSeries[bar] = ff;

			var f = _linRegSlope.Calculate(bar, ff);
			var rg = NikedInertia(bar, _ffSeries) ?? _inertiaFf[bar - 1];
			_inertiaFf[bar] = rg;

			var reg = NikedInertia(bar, _closeSeries) ?? candle.Close;
			var ls = _linRegSlopeClose.Calculate(bar, candle.Close);

			_hh[bar] = _highestClose[bar];
			_ll[bar] = _lowestClose[bar];

			if (bar <= _targetBar)
				return;

			var ffSqSum = 0m;

			for (var i = bar; i > Math.Max(bar - Period, 0); i--)
				ffSqSum += _ffSeries[i] * _ffSeries[i];

			var ffSum = _ffSeries.CalcSum(Period, bar);

			var sumQ = (double)(ffSqSum - 2 * rg * ffSum + rg * rg * Period);

			var skReg = (decimal)Math.Sqrt(sumQ / Period);
			var longValue = Period * f / skReg;
			_longValues[bar] = longValue;

			_grH[bar] = longValue < 1.5m
				? 1.5m
				: longValue > 1.6m
					? 1.6m
					: _grH[bar - 1];

			_grL[bar] = longValue > -1.5m
				? -1.5m
				: longValue < -1.6m
					? -1.6m
					: _grL[bar - 1];

			_dopnu[bar] = longValue > 0
				? 0
				: longValue < _grH[bar]
					? 1
					: _dopnu[bar - 1];

			var nuCrosses = _longValues[bar] < 1.5m && _longValues[bar - 1] >= 1.5m;
			
			_nu[bar] = nuCrosses && _grH[bar - 1] == 1.6m && _dopnu[bar] == 0 && candle.Close < _highestClose[bar]
				? 1
				: _nu[bar - 1] + 1;

			_dopnd[bar] = longValue < 0
				? 0
				: longValue > _grL[bar]
					? 1
					: _dopnd[bar - 1];

			var ndCrosses = _longValues[bar] > -1.5m && _longValues[bar - 1] <= -1.5m;

			_nd[bar] = ndCrosses && _grL[bar - 1] == -1.6m && _dopnd[bar] == 0 && candle.Close > _lowestClose[bar]
				? 1
				: _nd[bar - 1] + 1;

			if (_nu[bar] == 1)
				_downArrows[bar] = candle.High + InstrumentInfo.TickSize;
			else
				_downArrows[bar] = 0;

			if (_nd[bar] == 1)
				_upArrows[bar] = candle.Low - InstrumentInfo.TickSize;
			else
				_upArrows[bar] = 0;

			var hhCrosses = _linRegSlope[bar] < 0 && _linRegSlope[bar - 1] >= 0;

			_hh[bar] = bar < Period
				? candle.Close
				: hhCrosses
					? maxs
					: Math.Max(maxs, _hh[bar - 1]);

			var llCrosses = _linRegSlope[bar] > 0 && _linRegSlope[bar - 1] <= 0;

			_ll[bar] = bar < Period
				? candle.Close
				: llCrosses
					? mins
					: Math.Min(mins, _ll[bar - 1]);

			if (_ll[bar] == 0)
				_ll[bar] = mins;

			var otn = (decimal)Math.Log10((double)_hh[bar] / (double)_ll[bar]);
			_highestOtn.Calculate(bar, otn);
			_lowestOtn.Calculate(bar, otn);

			var stoch = _highestOtn[bar] == _lowestOtn[bar]
				? 1
				: (otn - _lowestOtn[bar]) / (_highestOtn[bar] - _lowestOtn[bar]);

			var cldh = bar < Period
				? candle.Close
				: stoch > 0.95m
					? _hh[bar]
					: _ll[bar];

			var cldl = bar < Period
				? candle.Close
				: _ll[bar];

			_cloud[bar] = new RangeValue
			{
				Lower = cldl,
				Upper = cldh
			};
		}

		#endregion

		#region Private methods

		private decimal? NikedInertia(int bar, ValueDataSeries series)
		{
			var sumXy = 0m;
			var sumX = 0m;
			var sumY = 0m;
			var sumSqrX = 0m;

			for (var i = bar; i >= Math.Max(bar - Period, 0); i--)
			{
				sumXy += i * series[i];
				sumX += i;
				sumY += series[i];
				sumSqrX += i * i;
			}

			try
			{
				var a = (Period * sumXy - sumX * sumY) / (Period * sumSqrX - sumX * sumX);
				var b = (sumSqrX * sumY - sumX * sumXy) / (Period * sumSqrX - sumX * sumX);
				return a * bar + b;
			}
			catch (DivideByZeroException)
			{
				return null;
			}
		}

		#endregion
	}
}