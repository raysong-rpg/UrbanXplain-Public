using UnityEngine;

namespace UrbanXplain
{
    /// <summary>
    /// 几何计算工具类，提供点在多边形内判断等功能
    /// </summary>
    public static class GeometryUtils
    {
        /// <summary>
        /// 判断一个点是否在多边形内部（使用射线法）
        /// 使用2D XZ平面计算（忽略Y轴）
        /// </summary>
        /// <param name="point">待测点的世界坐标</param>
        /// <param name="polygon">多边形顶点数组（世界坐标）</param>
        /// <returns>true = 点在多边形内部，false = 点在多边形外部</returns>
        public static bool IsPointInPolygon(Vector3 point, Vector3[] polygon)
        {
            if (polygon == null || polygon.Length < 3)
            {
                Debug.LogWarning("GeometryUtils: Polygon must have at least 3 vertices.");
                return false;
            }

            // 射线法：从点向右发射水平射线，统计与多边形边的交点数
            // 奇数 = 在内部，偶数 = 在外部
            // 使用2D XZ平面计算（忽略Y轴）

            int intersectionCount = 0;
            int vertexCount = polygon.Length;

            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 vertex1 = polygon[i];
                Vector3 vertex2 = polygon[(i + 1) % vertexCount]; // 最后一个顶点连接到第一个顶点

                // 使用X和Z坐标（忽略Y轴）
                float x1 = vertex1.x;
                float z1 = vertex1.z;
                float x2 = vertex2.x;
                float z2 = vertex2.z;
                float px = point.x;
                float pz = point.z;

                // 检查这条边是否与从点向右的水平射线相交
                // 条件1：边的两个端点在射线的上下两侧
                if ((z1 > pz) != (z2 > pz))
                {
                    // 条件2：交点在点的右侧
                    // 计算交点的X坐标
                    float intersectionX = x1 + (pz - z1) * (x2 - x1) / (z2 - z1);

                    if (px < intersectionX)
                    {
                        intersectionCount++;
                    }
                }
            }

            // 奇数个交点 = 在内部
            return (intersectionCount % 2) == 1;
        }

        /// <summary>
        /// 计算地块的中心点（从起始点和终点计算）
        /// </summary>
        /// <param name="startPos">起始点坐标</param>
        /// <param name="endPos">终点坐标</param>
        /// <returns>中心点坐标</returns>
        public static Vector3 CalculatePlotCenter(Vector3 startPos, Vector3 endPos)
        {
            return new Vector3(
                (startPos.x + endPos.x) / 2f,
                (startPos.y + endPos.y) / 2f,
                (startPos.z + endPos.z) / 2f
            );
        }
    }
}
