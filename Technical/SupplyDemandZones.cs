namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Context;

	public class SupplyDemandZones : Indicator
	{
		#region Nested types

		public class UpSwingRow
		{
			#region Properties

			public decimal Hh { get; set; }

			public decimal Hl { get; set; }

			public int Ihh { get; set; }

			public int EndBar { get; set; }

			#endregion
		}

		#endregion

		#region Fields

		private ValueDataSeries _buffDotDown = new("BuffDotDown");
		private ValueDataSeries _buffDotUp = new("BuffDotUp");
		private ValueDataSeries _buffDown = new("BuffDown");

		private ValueDataSeries _buffUp = new("BuffUp");
		private int _days;
		private decimal _highestLow;
		private int _lastBar;
		private decimal _lowestHigh;
		private List<UpSwingRow> _sup = new();
		private List<UpSwingRow> _supToDem = new();
		private int _targetBar;
		private List<UpSwingRow> _upswg = new();
		private bool _upSwing;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Days", GroupName = "Calculation", Order = 100)]
		[Range(0, 10000)]
		public int Days
		{
			get => _days;
			set
			{
				_days = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SupplyDemandZones()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			((ValueDataSeries)DataSeries[0]).IsHidden = true;
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			foreach (var supZone in _sup)
			{
				if (supZone.Ihh > LastVisibleBarNumber || supZone.Hl > ChartInfo.PriceChartContainer.High && supZone.Hh > ChartInfo.PriceChartContainer.Low)
					continue;

				var x1 = ChartInfo.GetXByBar(supZone.Ihh);
				var y1 = ChartInfo.GetYByPrice(supZone.Hh);

				var y2 = ChartInfo.GetYByPrice(supZone.Hl);

				var rect = new Rectangle(x1, y1, Container.Region.Width - x1, y2 - y1);
				context.FillRectangle(Color.IndianRed, rect);
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_upswg.Clear();
				_sup.Clear();
				_supToDem.Clear();

				_buffUp.Clear();
				_buffDown.Clear();
				_buffDotUp.Clear();
				_buffDotDown.Clear();

				_targetBar = 0;
				_upSwing = true;

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
				}

				return;
			}

			if (bar < _targetBar)
				return;

			var candle = GetCandle(bar);

			if (bar == _targetBar)
			{
				_highestLow = candle.Low;
				_lowestHigh = candle.High;
			}

			if (_upSwing)
			{
				if (candle.Low > _highestLow)
					_highestLow = candle.Low;

				if (candle.High < _highestLow)
				{
					_upSwing = false;
					_lowestHigh = candle.High;
				}
			}
			else
			{
				if (candle.High < _lowestHigh)
					_lowestHigh = candle.High;

				if (candle.Low > _lowestHigh)
				{
					_upSwing = true;
					_highestLow = candle.Low;
				}
			}

			if (_upSwing)
			{
				_buffUp[bar] = _highestLow;
				_buffDown[bar] = 0;

				if (candle.Low > _lowestHigh && _buffDown[bar - 1] != 0)
				{
					_buffDotUp[bar - 1] = _lowestHigh;
					_buffDotUp[bar] = _highestLow;
				}
			}
			else
			{
				_buffUp[bar] = 0;
				_buffDown[bar] = _lowestHigh;

				if (candle.High < _highestLow && _buffUp[bar - 1] != 0)
				{
					_buffDotDown[bar] = _lowestHigh;
					_buffDotUp[bar - 1] = _highestLow;
				}
			}

			if (_lastBar != bar && bar == CurrentBar - 1)
				GetSd();

			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private void GetSd()
		{
			var ihh = 0;
			var ihl = 0;
			var iesw = 0;
			var hh = 0m;
			var hl = 0m;

			bool broken, testPrev;

			for (var i = _targetBar; i < CurrentBar - 1; i++)
			{
				if (_buffDotUp[i] != 0 && _buffDotDown[i - 1] != 0)
				{
					for (var j = i + 1; j < CurrentBar - 1; j++)
					{
						if (_buffDotUp[j] != 0 && _buffDotDown[j + 1] != 0)
						{
							ihh = HighestBar(true, j - i + 1, j);
							ihl = HighestBar(false, j - i + 1, j);

							hh = GetCandle(ihh).High;
							hl = GetCandle(ihl).Low;

							var prevHigh = GetCandle(i - 1).High;

							if (prevHigh > hh)
							{
								hh = prevHigh;
								ihh = i - 1;
							}

							_upswg.Add(new UpSwingRow
							{
								Hh = hh,
								Hl = hl,
								Ihh = ihh,
								EndBar = j + 1
							});

							i = j + 1;
							break;
						}
					}
				}
			}

			for (var i = 0; i < _upswg.Count; i++)
			{
				hh = _upswg[i].Hh;
				hl = _upswg[i].Hl;
				ihh = _upswg[i].Ihh;
				iesw = _upswg[i].EndBar;

				broken = false;

				var j = 0;

				for (j = iesw + 1; j < CurrentBar - 1; j++)
				{
					var candle = GetCandle(j);

					if (candle.High > hh)
					{
						broken = true;
						break;
					}
				}

				if (!broken)
				{
					testPrev = false;

					for (var k = _sup.Count - 1; k >= 0; k--)
					{
						testPrev = hh <= _sup[k].Hh && hh >= _sup[k].Hl;

						if (testPrev)
							break;
					}

					if (!testPrev)
					{
						_sup.Add(new UpSwingRow
						{
							Hh = hh,
							Hl = hl,
							Ihh = ihh,
							EndBar = i
						});
					}
				}
				else
				{
					broken = false;

					for (var k = j + 1; k < CurrentBar - 1; k++)
					{
						if (GetCandle(k).Low < hl)
						{
							broken = true;
							break;
						}
					}

					if (!broken)
					{
						_supToDem.Add(new UpSwingRow
						{
							Hh = hh,
							Hl = hl,
							Ihh = ihh,
							EndBar = i
						});
					}
				}
			}
		}

		private int HighestBar(bool isHigh, int period, int bar)
		{
			var maxValue = 0m;
			var iHigh = 0;

			for (var i = bar; i >= Math.Max(0, bar - period); i--)
			{
				var candle = GetCandle(i);
				var value = isHigh ? candle.High : candle.Low;

				if (value <= maxValue)
					continue;

				maxValue = value;
				iHigh = i;
			}

			return iHigh;
		}

		#endregion
	}
}