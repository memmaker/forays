 /*Copyright (c) 2013-2015  Derrick Creamer
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.*/
using System;
using System.Collections;
namespace PosArrays{
	public struct pos{ //a generic position struct
		public int row;
		public int col;
		public pos(int r,int c){
			row = r;
			col = c;
		}
	}
	public class PosArray<T>{ //a 2D array with a position indexer and 1D indexer in addition to the usual 2D indexer
		//RVIP: a 1D array inside. The .NET wasm (Mono) interpreter threw ArrayTypeMismatchException
		//when storing into the generic 2D array (tile[i,j] = null in Map.InitializeNewLevel).
		public T[] data;
		public int Rows, Cols;
		public int GetLength(int dim){ return dim == 0? Rows : Cols; }
		public int GetUpperBound(int dim){ return GetLength(dim) - 1; }
		public PosArray<T> objs{ get{ return this; } } //callers use objs.GetLength(0/1)
		public T this[int row,int col]{
			get{
				return data[row*Cols + col];
			}
			set{
				data[row*Cols + col] = value;
			}
		}
		public T this[pos p]{
			get{
 				return data[p.row*Cols + p.col];
			}
			set{
				data[p.row*Cols + p.col] = value;
			}
		}
		public T this[int idx]{
			get{
				return data[idx];
			}
			set{
				data[idx] = value;
			}
		}
		public IEnumerator GetEnumerator(){
			return data.GetEnumerator();
		}
		public PosArray(int rows,int cols){
			Rows = rows;
			Cols = cols;
			data = new T[rows*cols];
		}
	}
}
