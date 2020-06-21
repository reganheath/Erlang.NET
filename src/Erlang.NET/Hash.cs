/*
 * Copyright 2020 Regan Heath
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.NET
{
	internal class Hash
	{
		private readonly uint[] abc = { 0, 0, 0 };

		/* Hash function suggested by Bob Jenkins.
		 * The same as in the Erlang VM (beam); utils.c.
		 */

		private static readonly uint[] HASH_CONST = {
				0, // not used
				0x9e3779b9, // the golden ratio; an arbitrary value
				0x3c6ef372, // (hashHConst[1] * 2) % (1<<32)
				0xdaa66d2b, //             1    3
				0x78dde6e4, //             1    4
				0x1715609d, //             1    5
				0xb54cda56, //             1    6
				0x5384540f, //             1    7
				0xf1bbcdc8, //             1    8
				0x8ff34781, //             1    9
				0x2e2ac13a, //             1    10
				0xcc623af3, //             1    11
				0x6a99b4ac, //             1    12
				0x08d12e65, //             1    13
				0xa708a81e, //             1    14
				0x454021d7, //             1    15
			};

		public Hash(int i)
		{
			abc[0] = abc[1] = HASH_CONST[i];
			abc[2] = 0;
		}

		//protected Hash() {
		//    Hash(1);
		//}

		private void Mix()
		{
			abc[0] -= abc[1]; abc[0] -= abc[2]; abc[0] ^= (abc[2] >> 13);
			abc[1] -= abc[2]; abc[1] -= abc[0]; abc[1] ^= (abc[0] << 8);
			abc[2] -= abc[0]; abc[2] -= abc[1]; abc[2] ^= (abc[1] >> 13);
			abc[0] -= abc[1]; abc[0] -= abc[2]; abc[0] ^= (abc[2] >> 12);
			abc[1] -= abc[2]; abc[1] -= abc[0]; abc[1] ^= (abc[0] << 16);
			abc[2] -= abc[0]; abc[2] -= abc[1]; abc[2] ^= (abc[1] >> 5);
			abc[0] -= abc[1]; abc[0] -= abc[2]; abc[0] ^= (abc[2] >> 3);
			abc[1] -= abc[2]; abc[1] -= abc[0]; abc[1] ^= (abc[0] << 10);
			abc[2] -= abc[0]; abc[2] -= abc[1]; abc[2] ^= (abc[1] >> 15);
		}

		public void Combine(int a)
		{
			abc[0] += (uint)a;
			Mix();
		}

		public void Combine(long a)
		{
			Combine((int)(((uint)a) >> 32), (int)a);
		}

		public void Combine(int a, int b)
		{
			abc[0] += (uint)a;
			abc[1] += (uint)b;
			Mix();
		}

		public void Combine(byte[] b)
		{
			int j, k;
			for (j = 0, k = 0;
				 j + 4 < b.Length;
				 j += 4, k += 1, k %= 3)
			{
				abc[k] += ((uint)b[j + 0] & 0xFF) + ((uint)b[j + 1] << 8 & 0xFF00)
				+ ((uint)b[j + 2] << 16 & 0xFF0000) + ((uint)b[j + 3] << 24);
				Mix();
			}
			for (int n = 0, m = 0xFF;
				 j < b.Length;
				 j++, n += 8, m <<= 8)
			{
				abc[k] += (uint)(b[j] << n & m);
			}
			Mix();
		}

		public int ValueOf()
		{
			return (int)abc[2];
		}
	}
}
