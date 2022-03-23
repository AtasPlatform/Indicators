namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

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
		public class DnSwingRow
		{
			#region Properties

			public decimal Ll { get; set; }

			public decimal Lh { get; set; }

			public int Ill { get; set; }

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
		private List<DnSwingRow> _dem = new();
		private List<UpSwingRow> _supToDem = new();
		private List<DnSwingRow> _demToSup = new();
		private int _targetBar;
		private List<UpSwingRow> _upswg = new();
		private List<DnSwingRow> _dnswg = new();
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

			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
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
				context.FillRectangle(Color.FromArgb(128, 0, 255, 0), rect);
			}

			foreach (var demZone in _dem)
			{
				if (demZone.Ill > LastVisibleBarNumber || demZone.Ll > ChartInfo.PriceChartContainer.High && demZone.Lh > ChartInfo.PriceChartContainer.Low)
					continue;

				var x1 = ChartInfo.GetXByBar(demZone.Ill);
				var y1 = ChartInfo.GetYByPrice(demZone.Lh);

				var y2 = ChartInfo.GetYByPrice(demZone.Ll);

				var rect = new Rectangle(x1, y1, Container.Region.Width - x1, y2 - y1);
				context.FillRectangle(Color.FromArgb(128, 255, 0, 0), rect);
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_upswg.Clear();
				_dnswg.Clear();
				_sup.Clear();
				_dem.Clear();
				_supToDem.Clear();
				_demToSup.Clear();

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
					_buffDotDown[bar - 1] = _lowestHigh;
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

			
			var ill = 0;
			var ilh = 0;
			var ll = 0m;
			var lh = 0m;

			for (var i = 0; i < CurrentBar - 1; i++)
			{
				if(_buffDotDown[i] != 0 && _buffDotUp[i-1] != 0)
					for (var j = i + 1; j < CurrentBar - 1; j++)
					{
						if (_buffDotDown[j] != 0 && _buffDotUp[j+1] != 0)
						{
							ill = LowestBar(true, j - i + 1, j);
							ilh = LowestBar(false, j - i + 1, j);
							ll = GetCandle(ill).Low;
							lh = GetCandle(ilh).High;

							var prevLow = GetCandle(i - 1).Low;

							if (prevLow < ll)
							{
								ll = prevLow;
								ill = i - 1;
							}

							_dnswg.Add(new DnSwingRow()
							{
								Ll = ll,
								Lh = lh,
								Ill = ill,
								EndBar = j + 1
							});

							i = j + 1;
							break;
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


			
			for (var i = 0; i < _dnswg.Count; i++)
			{
				ll = _dnswg[i].Ll;
				lh = _dnswg[i].Lh;
				ill = _dnswg[i].Ill;
				iesw = _dnswg[i].EndBar;

				broken = false;
				var j = 0;

				for (j = iesw + 1; j < CurrentBar - 1; j++)
				{
					var candle = GetCandle(j);

					if (candle.Low < ll)
					{
						broken = true;
						break;
					}

					if (!broken)
					{
						testPrev = false;

						for (var k = _dem.Count - 1; k >= 0; k--)
						{
							testPrev = ll >= _dem[k].Ll && ll <= _dem[k].Lh;

							if(testPrev)
								break;
						}

						if (!testPrev)
						{
							_dem.Add(new DnSwingRow()
							{
								Ll = ll,
								Lh = lh,
								Ill = ill,
								EndBar = i
							});
						}
					}
					else
					{
						broken = false;

						for (var k = j + 1; k < CurrentBar - 1; k++)
						{
							if (GetCandle(k).High > lh)
							{
								broken = true;
								break;
							}
						}

						if (!broken)
						{
							_demToSup.Add(new DnSwingRow()
							{
								Ll = ll,
								Lh = lh,
								Ill = ill,
								EndBar = i
							});
						}
					}
				}
			}
			
		}

		private int LowestBar(bool isLow, int period, int bar)
		{
			var minValue = 0m;
			var iLow = 0;

			for (var i = bar; i > Math.Max(0, bar - period); i--)
			{
				var candle = GetCandle(i);
				var value = isLow ? candle.Low : candle.High;

				if (value >= minValue && minValue != 0)
					continue;

				minValue = value;
				iLow = i;
			}

			return iLow;
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