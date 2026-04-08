import pandas as pd

# 定义输入和输出的文件路径（使用原始字符串以避免转义问题）
input_file = r"C:\Users\Lvy\Desktop\111\ScoreLogs.csv"
output_file = r"C:\Users\Lvy\Desktop\111\OptScoreLogs.csv"

# 读取 CSV 文件
df = pd.read_csv(input_file)

# 1. 剔除无效数据：如果 Used 列为 0，则剔除此数据
df = df[df['Used'] != 0].reset_index(drop=True)

# 2. 判断高效合作：当 Used 与 Required 相等时认为是高效合作
df['Efficient'] = df['Used'] == df['Required']

# 3. 直接从第一条开始记录
# 记录累计有效数据数（从1开始），以及累计高效合作数据数，计算动态高效合作率
df['ValidCount'] = range(1, len(df) + 1)
df['EfficientCount'] = df['Efficient'].astype(int).cumsum()
df['DynamicEfficiency'] = df['EfficientCount'] / df['ValidCount']

# 将处理后的数据写入新 CSV 文件
df.to_csv(output_file, index=False)

print(f"数据处理完毕，新文件生成为 {output_file}")