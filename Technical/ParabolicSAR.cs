namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Attributes.Editors;

	using Utils.Common.Localization;

	[DisplayName("ParabolicSAR")]
	[LocalizedDescription(typeof(Resources), "ParabolicSAR")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/8589-parabolic-sar")]
	public class ParabolicSAR : Indicator
	{
		#region Fields

		private decimal _accel;
		private decimal _accelMax;
		private decimal _accelStart;
		private decimal _accelStep;
		private decimal _current;

		private bool _isDown;
		private bool _isIncreased;
		private int _lastbar;
		private decimal _prev;
		private decimal _revers;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "AccelStart",
			GroupName = "Common",
			Order = 20)]
		public decimal AccelStart
		{
			get => _accelStart;
			set
			{
				if (value <= 0)
					return;

				_accelStart = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "AccelStep",
			GroupName = "Common",
			Order = 21)]
		public decimal AccelStep
		{
			get => _accelStep;
			set
			{
				if (value <= 0)
					return;

				_accelStep = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "AccelMax",
			GroupName = "Common",
			Order = 22)]
		public decimal AccelMax
		{
			get => _accelMax;
			set
			{
				if (value <= 0)
					return;

				_accelMax = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ParabolicSAR()
			: base(true)
		{
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Dots;
			((ValueDataSeries)DataSeries[0]).Color = Colors.Blue;
			((ValueDataSeries)DataSeries[0]).Width = 2;

			AccelStart = 0.02m;
			AccelStep = 0.02m;
			AccelMax = 0.2m;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0 || bar == _lastbar)
				return;

			_lastbar = bar;
			Process(bar - 1);
		}

		#endregion

		#region Private methods

		private void Process(int bar)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				_isDown = true;
				_isIncreased = true;

				_accel = AccelStart;

				_prev = candle.Low;
				_current = candle.High;
				_revers = candle.Low;

				return;
			}

			var prevCandle = GetCandle(bar - 1);

			if (!_isDown)
			{
				if (candle.High > _prev)
				{
					_isDown = true;
					_isIncreased = true;

					_prev = _revers;
					_current = candle.High;

					_accel = AccelStart;

					this[bar] = _prev;
				}
				else
				{
					if (candle.Low < _revers)
					{
						_revers = candle.Low;

						if (!_isIncreased)
						{
							_accel += AccelStep;

							if (_accel > AccelMax)
								_accel = AccelMax;
						}
					}

					_isIncreased = false;
					_prev += (_revers - _prev) * _accel;

					var maxHigh = Math.Max(candle.High, prevCandle.High);

					if (_prev < maxHigh)
						_prev = maxHigh;

					this[bar] = _prev;
				}
			}
			else
			{
				if (candle.Low >= _prev)
				{
					if (candle.High > _current)
					{
						_current = candle.High;

						if (!_isIncreased)
						{
							_accel += AccelStart;

							if (_accel > AccelMax)
								_accel = AccelMax;
						}
					}

					_isIncreased = false;
					_prev += (_current - _prev) * _accel;

					var minLow = Math.Min(candle.Low, prevCandle.Low);

					if (_prev > minLow)
						_prev = minLow;

					this[bar] = _prev;
				}
				else
				{
					_isDown = false;
					_isIncreased = true;

					_prev = _current;
					_revers = candle.Low;

					_accel = AccelStep;

					this[bar] = _prev;
				}
			}
		}

		#endregion
	}
}