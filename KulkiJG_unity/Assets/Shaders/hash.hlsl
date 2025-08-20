static const int2 translations[9] =
{
	int2(-1, 1),
	int2(0, 1),
	int2(1, 1),
	int2(-1, 0),
	int2(0, 0),
	int2(1, 0),
	int2(-1, -1),
	int2(0, -1),
	int2(1, -1),
};

static const uint prime1 = 193;
static const uint prime2 = 389;

uint HashFromCell(uint2 cell, uint HashCount)
{
    uint hash = prime1 * cell.x + prime2 * cell.y;
    return hash % HashCount;
}

uint2 posToCell(float2 pos, float2 box_size, float2 cell_size)
{
    uint2 cell = (uint2)floor((pos + box_size/2)/cell_size);
    return cell;
}

uint HashFromPos(float2 pos, float2 box_size, float2 cell_size, int HashCount)
{
    uint2 cell = posToCell(pos, box_size, cell_size);
    return HashFromCell(cell, HashCount);
}
