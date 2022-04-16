using Open.Disposable;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Open.Collections.Numeric;

public class PossibleAddends : DisposableBase
{
	readonly ConcurrentDictionary<int, ConcurrentDictionary<int, IReadOnlyList<IReadOnlyList<int>>>> Cache = new();

	public IReadOnlyList<IReadOnlyList<int>> UniqueAddendsFor(int sum, int count)
	{
		_ = AssertIsAlive(true);

		return Cache
			.GetOrAdd(count, _ => new ConcurrentDictionary<int, IReadOnlyList<IReadOnlyList<int>>>())
			.GetOrAdd(sum, _ => GetUniqueAddends(sum, count).Memoize());
	}

	public IEnumerable<IReadOnlyList<int>> GetUniqueAddends(int sum, int count)
	{
		return count > int.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot be greater than signed 32 bit integer maximum.")
			: count < 2 || sum < 3 ? Enumerable.Empty<IReadOnlyList<int>>()
			: GetUniqueAddendsCore(sum, count);

		IEnumerable<IReadOnlyList<int>> GetUniqueAddendsCore(int sum, int count)
		{
			if (count == 2)
			{
				int i = 0;
			loop2:
				i++;
				if (i * 2 >= sum) yield break;
				yield return ImmutableArray.Create(i, sum - i);

				goto loop2;
			}

			{
				int i = 2;
				var c1 = count - 1;
				var c2 = c1 - 1;
				var builder = ImmutableArray.CreateBuilder<int>();

				while (++i < sum)
				{
					var next = sum - i;
					var addends = UniqueAddendsFor(i, c1);
					foreach (var a in addends)
					{
						builder.Capacity = count;
						if (a[c2] >= next) continue;
						builder.AddRange(a);
						builder.Add(next);
						yield return builder.MoveToImmutable();
					}
				}
			}
		}
	}

	protected override void OnDispose()
	{
		foreach (var c in Cache.Values)
		{
			foreach (var s in c.Values)
			{
				if (s is IDisposable d) d.Dispose();
			}
		}

		Cache.Clear();
	}

	public static IEnumerable<int[]> GetUniqueAddendsBuffered(int sum, int count)
	{
		return count > int.MaxValue ? throw new ArgumentOutOfRangeException(nameof(count), count, "Cannot be greater than signed 32 bit integer maximum.")
			: count < 2 || sum < 3 ? Enumerable.Empty<int[]>()
			: GetUniqueAddendsBufferedCore(sum, count);

		static IEnumerable<int[]> GetUniqueAddendsBufferedCore(int sum, int count)
		{
			var pool = ArrayPool<int>.Shared;
			var result = pool.Rent(count);

			try
			{
				if (count == 2)
				{
					int i = 0;
				loop2:
					i++;
					if (i * 2 >= sum) yield break;
					result[0] = i;
					result[1] = sum - i;
					yield return result;

					goto loop2;
				}

				{
					int i = 2;
					var c1 = count - 1;
					var c2 = c1 - 1;

					while (++i < sum)
					{
						var next = sum - i;
						var addends = GetUniqueAddendsBuffered(i, c1);
						foreach (var a in addends)
						{
							if (a[c2] >= next) continue;
							a.CopyTo(result, 0);
							result[c1] = next;
							yield return result;
						}
					}
				}
			}
			finally
			{
				pool.Return(result);
			}
		}
	}

	public static IEnumerable<IEnumerable<int>> GetUniqueAddendsEnumerable(int sum, int count)
		=> GetUniqueAddendsBuffered(sum, count).Select(a => a.Take(count));
}
