import time
from enum import Enum
from abc import abstractmethod
import sys
import os.path
import sqlite3
# Wand: https://docs.wand-py.org/en/0.6.11/
from wand.image import Image
import constants


class DataType(str, Enum):
    GPKG = 'gpkg'
    FS = 'fs'
    S3 = 's3'
    
    @classmethod
    def has_value(cls, value):
        return value in cls._value2member_map_


class Data():
    def __init__(self, type: DataType, path) -> None:
        self.type = type
        self.path = path
        self.num_tiles = self.__get_num_tiles__()
    
    @abstractmethod
    def get_all_data(self) -> list:
        pass
    
    @abstractmethod
    def get_blob(self, z, x, y) -> str:
        pass
    
    @abstractmethod
    def __get_num_tiles__(self) -> int:
        pass
    
    def compare(self, other: 'Data') -> bool:
        count = 0
        
        # if self.num_tiles != other.num_tiles:
        #     return 0
        
        rows = self.get_all_data()
        for _, z, x, y, data in rows:
            blob = other.get_blob(z, x, y)
            if not blob:
                break
            
            similar: bool = are_images_similar(data, blob)
            if not similar:
                print(z, x, y)
                break
            
            count += 1
        
        return count / self.num_tiles


class Gpkg(Data):
    def __init__(self, path) -> None:
        self.connection: sqlite3.Connection = sqlite3.connect(path)
        self.cursor = self.connection.cursor()
        self.data_table = self.__get_data_table_name__()
        super().__init__(DataType.GPKG, path)
    
    def __enter__(self):
        return self
    
    def __get_data_table_name__(self) -> str:
        return self.cursor.execute(f'select * from gpkg_contents').fetchone()[0]
    
    def get_all_data(self) -> list:
        return self.cursor.execute(f'select * from {self.data_table}').fetchall()
    
    def get_blob(self, z, x, y) -> str:
        return self.cursor.execute(f"select coalesce(tile_data, '') from {self.data_table} where zoom_level={z} and tile_column={x} and tile_row={y}").fetchone()
    
    def __get_num_tiles__(self) -> int:
        return self.cursor.execute(f'select count(*) from {self.data_table}').fetchone()[0]

    def __exit__(self, exc_type, exc_value, traceback):
        self.cursor.close()
        self.connection.close()


class FS(Data):
    def __init__(self, path) -> None:
        super().__init__(DataType.FS, path)
    
    def __enter__(self):
        return self
    
    def get_blob(self, z, x, y) -> str:
        return super().get_blob(z, x, y)
    
    def __exit__(self, exc_type, exc_value, traceback):
        pass


class S3(Data):
    def __init__(self, path) -> None:
        super().__init__(DataType.S3, path)
    
    def __enter__(self):
        return self
    
    def get_blob(self, z, x, y) -> str:
        return super().get_blob(z, x, y)
    
    def __exit__(self, exc_type, exc_value, traceback):
        pass


def get_gpkg_table_name(cursor: sqlite3.Cursor):
    return cursor.execute(f"select * from gpkg_contents").fetchone()[0]


def are_images_similar(blob1, blob2):
    with Image(blob=blob1) as img1, Image(blob=blob2) as img2:
        # print('format =', img1.format)
        # img1.format = 'png'
        # print('format =', img2.format)
        # img2.format = 'png'
        # print('format =', img1.format)
        
        # print('width =', img1.width)
        # print('height =', img1.height)
        # print('width =', img2.width)
        # print('height =', img2.height)
        
        # print(img1.size)
        
        # if img1.format != img2.format:
        #     if img2.format == 'jpeg':
        #         temp = img1
        #         img1 = img2
        #         img2 = temp
        #     img1.format = 'png'
        #     img1.compose = 'copy_opacity'
        #     img1.composite(img2, operator='copy_opacity')
        
        # print(False if img1.compare(img2)[1] > 1 else True)
        
        # img1.compose = 'copy_opacity'
        # img1.composite(img2)
        
        # print(img1.get_image_distortion(img2))
        
        if img1.get_image_distortion(img2) < constants.TILE_DIFF_THRESHOLD:
            # img1.format = 'png'
            # img2.format = 'png'
            # img1.compose = 'copy_opacity'
            # img2.compose = 'copy_opacity'
            # img1.composite(img2)
            # img1.fuzz = img1.quantum_range * 0.2
            # img2.fuzz = img2.quantum_range * 0.2
            # img1.transparent_color(Color('rgb(0,0,0)'), 0.0)
            # img2.transparent_color(Color('rgb(0,0,0)'), 0.0)
            # img1.composite(img2, operator='copy_opacity')
            # img1.composite_channel('undefined', img2, 'copy_opacity')
            # img1.trim(Color('rgb(0,0,0)'))
            # img2.trim(Color('rgb(0,0,0)'))
        
            # print('quantum_range =', img1.quantum_range)
            # similarity = img1.similarity(img2, metric='measn_squared')[1]
            # print('similarity =', img1.similarity(img2, threshold=img1.quantum_range, metric='mean_squared')[1])
            # print('similarity =', similarity)
            # print('RMSE = ', math.sqrt(similarity))
            
            # TODO: remove this
            # print('distortion =', img1.get_image_distortion(img2, metric='mean_error_per_pixel') / math.prod(img1.size))
            # print('distortion =', img1.get_image_distortion(img2, metric='mean_error_per_pixel'))
            # print('distortion =', img1.get_image_distortion(img2))
            # print('size = ', math.prod(img1.size))
            # print('compare =', img1.compare(img2)[1])
            
            # img1.save(filename='1.png')
            # img2.save(filename='2.png')
        
            # print(img1.compare(img2)[1])
            return False
        return True


def create_data(type: str, path: str):
    return {
        DataType.GPKG: Gpkg(path),
        DataType.FS: FS(path),
        DataType.S3: S3(path)
    }[type]


def valid_type(type: str):
    return True if DataType.has_value(type) else False


if __name__ == "__main__":
    data_type1 = sys.argv[1]
    data_path1 = sys.argv[2]
    data_type2 = sys.argv[3]
    data_path2 = sys.argv[4]
    
    if not os.path.exists(data_path1):
        print(f'File {data_path1} does not exist')
        exit(1)
        
    if not os.path.exists(data_path2):
        print(f'File {data_path2} does not exist')
        exit(1)
    
    if not valid_type(data_type1):
        print(f'Invalid data type {data_type1}')
        exit(1)
    
    if not valid_type(data_type2):
        print(f'Invalid data type {data_type2}')
        exit(1)
    
    # start = time.time()
    with create_data(data_type1, data_path1) as data1, create_data(data_type2, data_path2) as data2:
        percentage = data1.compare(data2)
    # end = time.time()
    # print(f'Run time: {end - start}')
    # print(f'Gpkgs {"do not " if percentage < 1 else ""}match, match percentage: {percentage}')
    # print(0 if percentage < 1 else 1)
