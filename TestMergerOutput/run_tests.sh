export S3_ACCESS_KEY="minio123" #"minioadmin"
export S3_SECRET_KEY="minio123" #"minioadmin"
export S3__url="http://localhost:9000"
export S3__bucket="tiles"
export GENERAL__uploadOnly=false

MAXIMUM_JEPG_TILES_IN_RAM=100000
MAXIMUM_PNG_TILES_IN_RAM=30000
TILE_DIFF_THRESHOLD=0.97

TEST_FOLDER=''
INPUT_FOLDER="${TEST_FOLDER}/input"
OUTPUT_FOLDER="${TEST_FOLDER}/output"
TEST_RESULTS="${TEST_FOLDER}/correct-results"

# OUTPUT_DATA_ARR=('gpkg')
# OUTPUT_FORMAT_ARR=('jpeg' 'png')
# BATCH_SIZE_ARR=(500 1000 2000 5000 10000 15000 20000)
# THREAD_NUM_ARR=(1 3 5 8 10 15)

OUTPUT_FORMAT_ARR=('jpeg')
BATCH_SIZE_ARR=(15000)
THREAD_NUM_ARR=(5)

GEO=('geo' '33.8882,29.20989,36.22737,33.6244')
AREA1=('area1' '34.2663935002085,31.1786148130457,34.3258795317408,31.23180570002')
AREA2=('area2' '34.3872622999935,31.3605353131281,34.4079770002155,31.3883042230593')
AREA3=('area3' '34.4874343000325,31.5761480127462,34.5199694953832,31.6105553899892')
JORD=('Jord' '35.70419410,31.96472261,35.81542877,32.04025328')
SYRIA=('Syria' '35.941774115,32.80380351,36.01043588,32.87933150')
TZOR=('Tzor' '35.1837230,33.22952440,35.23727232,33.2968062')
MERGED=('merged' '33.8882,29.20989,36.22737,33.6244')
TILE=('tile' '33.8882,29.20989,36.22737,33.6244')

# DATA_TO_CHECK=('GEO[@]' 'TZOR[@]' 'JORD[@]' 'SYRIA[@]' 'AREA1[@]' 'AREA2[@]' 'AREA3[@]')
DATA_TO_CHECK=('AREA1[@]')

RESULTS=()

test -z $TEST_FOLDER && echo "Please assign value to TEST_FOLDER" && exit

function run_tests {
    OUTPUT_FILE_TYPE=$1
    BATCH_SIZE=$2


    echo "
##########
Output format: $OUTPUT_FILE_TYPE
Batch size: $BATCH_SIZE
Threads: $GENERAL__parallel__numOfThreads
##########" >> run.txt

    ## Export to new data target

    ### GPKG target
    for data in "${DATA_TO_CHECK[@]}"
    do
        STARTTIME=$(date +%s)

        IFS=' ' read -ra data_arr <<< "${!data}"
        dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE gpkg $OUTPUT_FOLDER/gpkgs/${data_arr[0]}.gpkg ${data_arr[1]} gpkg $INPUT_FOLDER/gpkgs/${data_arr[0]}.gpkg >> run.txt
        RESULTS+=($(python3 TestMergerOutput/run_tests.py gpkg $OUTPUT_FOLDER/gpkgs/${data_arr[0]}.gpkg gpkg $TEST_RESULTS/gpkgs/${data_arr[0]}_${OUTPUT_FILE_TYPE}.gpkg))
        rm -f $OUTPUT_FOLDER/gpkgs/${data_arr[0]}.gpkg

        ENDTIME=$(date +%s)
        echo "It takes $(($ENDTIME - $STARTTIME)) seconds to complete run and check for ${data_arr[0]}..."
    done
    
    dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE gpkg $OUTPUT_FOLDER/gpkgs/${MERGED[0]}.gpkg ${MERGED[1]} gpkg $INPUT_FOLDER/gpkgs/${GEO[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${TZOR[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${JORD[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${SYRIA[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${AREA1[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${AREA2[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${AREA3[0]}.gpkg >> run.txt
    RESULTS+=($(python3 TestMergerOutput/run_tests.py gpkg $OUTPUT_FOLDER/gpkgs/${MERGED[0]}.gpkg gpkg $TEST_RESULTS/gpkgs/${MERGED[0]}_${OUTPUT_FILE_TYPE}.gpkg))
    rm -f $OUTPUT_FOLDER/gpkgs/${MERGED[0]}.gpkg

    ### FS target
    # dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE fs $OUTPUT_FOLDER/tiles/area1 ${AREA1[1]} gpkg $INPUT_FOLDER/gpkgs/${AREA1[0]}
    # dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE fs $OUTPUT_FOLDER/tiles/area2 ${AREA2[1]} gpkg $INPUT_FOLDER/gpkgs/${AREA2[0]}
    # dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE fs $OUTPUT_FOLDER/tiles/area3 ${AREA3[1]} gpkg $INPUT_FOLDER/gpkgs/${AREA3[0]}

    ### S3 target
    # dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE s3 $OUTPUT_FOLDER/tiles/area1 ${AREA1[1]} gpkg $INPUT_FOLDER/gpkgs/${AREA1[0]}
    # dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE s3 $OUTPUT_FOLDER/tiles/area2 ${AREA2[1]} gpkg $INPUT_FOLDER/gpkgs/${AREA2[0]}
    # dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE s3 $OUTPUT_FOLDER/tiles/area3 ${AREA3[1]} gpkg $INPUT_FOLDER/gpkgs/${AREA3[0]}
}

# Run CLI tests
for format in "${OUTPUT_FORMAT_ARR[@]}"
do
    for batch_size in "${BATCH_SIZE_ARR[@]}"
    do
        for thread_num in "${THREAD_NUM_ARR[@]}"
        do
            # Skip batches that will be an issue because of RAM limits
            if [[ $format == 'jpeg' ]] && (( $(echo "$thread_num * $batch_size > $MAXIMUM_JEPG_TILES_IN_RAM" | bc -l) )); then
                echo "Skipping due to RAM limit: $format, Threads: $thread_num, Batch size: $batch_size"
                break
            fi
            if [[ $format == 'png' ]] && (( $(echo "$thread_num * $batch_size > $MAXIMUM_PNG_TILES_IN_RAM" | bc -l) )); then
                echo "Skipping due to RAM limit: $format, Threads: $thread_num, Batch size: $batch_size"
                break
            fi

            export GENERAL__parallel__numOfThreads=$thread_num
            # echo "Format: $format, Threads: $thread_num, Batch size: $batch_size"
            run_tests "$format" $batch_size
        done
    done
done

echo "Results: ${RESULTS[@]}"

# Get run times
#cat run.txt | grep "Total runtime:" | awk 'NR % 4 == 0' | awk -F' ' '{print $NF}' | awk -F: '{print $NF}'
# Get run times in sec
#cat run.txt | grep "Total runtime:" | awk 'NR % 4 == 1' | awk -F' ' '{print $NF}' | awk -F: '{print $3 + $2 * 60 + $1 * 60 * 60}'

OFFSET=0

get_gpkg_table_name() {
    GPKG_INFO_TABLE='gpkg_contents'
    GPKG=$1

    GPKG_CONTENT=$(sqlite3 $GPKG "select * from $GPKG_INFO_TABLE")
    IFS='|' read -r -a GPKG_CONTENT_ARR <<<$GPKG_CONTENT
    GPKG_TABLE_NAME=${GPKG_CONTENT_ARR[0]}

    echo $GPKG_TABLE_NAME
}

is_png() {
    BLOB=$1

    # Check if BLOB is png
    if [[ $BLOB =~ ^89504E470D0A1A0A ]]; then
        echo 1
    else
        echo 0
    fi
}

get_type() {
    BLOB="$1"

    if [ "$(is_png "$BLOB")" -eq 1 ]; then
        echo "png"
    else
        echo "jpeg"
    fi
}

# get_as_jpeg_blob() {
#     BLOB="$1"

#     if [ "$(is_png "$BLOB")" -eq 1 ]; then
#         # Write blob to file
#         echo "$BLOB" | xxd -r -p - temp.png

#         # Convert from png to jpeg
#         convert temp.png temp.jpeg

#         BLOB="$(xxd -p temp.jpeg)"
#         rm temp.png temp.jpeg
#     fi
# }

are_similar() {
    BLOB1="$1"
    BLOB2="$2"

    TYPE1=$(get_type "$BLOB1")
    TYPE2=$(get_type "$BLOB2")

    # if [[ $TYPE1 != $TYPE2 ]]; then
    #     if [[ $TYPE1 == 'png' ]]; then
    #         get_as_jpeg_blob "$BLOB1"
    #         BLOB1="$BLOB"
    #         TYPE1='jpeg'
    #     else
    #         get_as_jpeg_blob "$BLOB2"
    #         BLOB2="$BLOB"
    #         TYPE2='jpeg'
    #     fi
    # fi

    # Save blobs to file
    echo "$BLOB1" | xxd -r -p - temp1.$TYPE1
    echo "$BLOB2" | xxd -r -p - temp2.$TYPE2

    # convert temp1.$TYPE1 temp1.jpeg
    # convert temp2.$TYPE2 temp2.jpeg

    # Image attributes: https://imagemagick.org/script/escape.php
    DIFF=$(compare -format "\n%[distortion]" temp1.$TYPE1 temp1.$TYPE1 - | tail -1)
    rm -f temp1.$TYPE1 temp2.$TYPE2 temp1.jpeg temp2.jpeg

    if (( $(echo "$DIFF > $TILE_DIFF_THRESHOLD" | bc -l) )); then
        return 1
    fi

    return 0
}

# are_similar() {
#     FILE1="$1"
#     FILE2="$2"
#     TYPE1="${FILE1##*.}"
#     TYPE2="${FILE2##*.}"

#     if [[ "$TYPE1" != 'jpeg' ]]; then
#         convert $FILE1 temp1.jpeg
#     fi

#     if [[ "$TYPE2" != 'jpeg' ]]; then
#         convert $FILE2 temp2.jpeg
#     fi

#     DIFF=$(compare -format "\n%[distortion]" temp1.jpeg temp1.jpeg - | tail -1)
#     # rm -f temp1.$TYPE1 temp2.$TYPE2 temp1.jpeg temp2.jpeg

#     if (( $(echo "$DIFF > $TILE_DIFF_THRESHOLD" | bc -l) )); then
#         return 1
#     fi

#     return 0
# }

do_gpkgs_match() {
    GPKG1=$(realpath $1)
    GPKG2=$(realpath $2)

    GPKG1_TABLE_NAME=$(get_gpkg_table_name $GPKG1)
    GPKG2_TABLE_NAME=$(get_gpkg_table_name $GPKG2)

    # Get tiles from GPKG1
    DATA=($(sqlite3 $GPKG1 "select zoom_level, tile_column, tile_row, hex(tile_data) from $GPKG1_TABLE_NAME limit $BATCH_SIZE offset $OFFSET"))

    # Get number of results
    RESULTS_SIZE=${#DATA[@]}

    # Loop as long as results are returned
    while [ $RESULTS_SIZE -gt 0 ]; do
        for row in ${DATA[@]}; do
            # Split row columns
            IFS='|' read -r -a ROW1 <<<$row
            BLOB1=${ROW1[3]}

            # Get corresponding BLOB
            BLOB2=$(sqlite3 $GPKG2 "select coalesce(hex(tile_data), '') from $GPKG2_TABLE_NAME where zoom_level=${ROW1[0]} and tile_column=${ROW1[1]} and tile_row=${ROW1[2]}")

            # If no result was returned (no such tile)
            if [[ "$BLOB2" == '' ]]; then
                echo "Empty corresponding tile\n" >> tmp.txt
                return 0
            fi

            # Save blobs to file
            TYPE1=$(get_type "$BLOB1")
            TYPE2=$(get_type "$BLOB2")
            echo "$BLOB1" | xxd -r -p - temp1.$TYPE1
            echo "$BLOB2" | xxd -r -p - temp2.$TYPE2

            are_similar temp1.$TYPE1 temp2.$TYPE2
            if [ $? -eq 0 ]; then
                echo "Blobs are not similar, zoom_level=${ROW1[0]} and tile_column=${ROW1[1]} and tile_row=${ROW1[2]}\n" >> tmp.txt
                return 0
            fi
        done

        RESULTS_SIZE=${#DATA[@]}
        ((OFFSET += RESULTS_SIZE))
        DATA=($(sqlite3 $GPKG1 "select zoom_level, tile_column, tile_row, hex(tile_data) from $GPKG1_TABLE_NAME limit $BATCH_SIZE offset $OFFSET"))
    done

    return 1
}


#########################
# time do_gpkgs_match $INPUT_FOLDER/gpkgs/area3.gpkg $OUTPUT_FOLDER/gpkgs/area3.gpkg
# echo $?

# BLOB=''

# if [[ "$row2" == '' ]]; then
#     # echo "1 zoom_level=${ROW1[0]} and tile_column=${ROW1[1]} and tile_row=${ROW1[2]}\n" >> tmp.txt
#     # echo "Empty corresponding tile\n" >> tmp.txt
#     echo 0
# fi

#########################
# convert TestMergerOutput/check.jpeg test1.jpeg
# convert TestMergerOutput/check.png test2.jpeg

# IMAGE1='TestMergerOutput/check1.jpeg'
# IMAGE2='TestMergerOutput/check1.png'
# BLOB1="$(xxd -p $IMAGE1)"
# BLOB2="$(xxd -p $IMAGE2)"

# compare -format "\n%[distortion]" - TestMergerOutput/other.png -
# DIFF=$(compare -format "\n%[distortion]" TestMergerOutput/check.jpeg TestMergerOutput/check.png - | tail -1)
# DIFF=$(compare -format "\n%[distortion]" temp1.jpeg temp2.jpeg - | tail -1)
# echo "diff: $DIFF"

# if (( $(echo "$DIFF > $TILE_DIFF_THRESHOLD" | bc -l) )); then
#     echo "same!"
# fi

# are_similar "$BLOB1" "$BLOB2"
# if [ $? -eq 0 ]; then
#     echo 0
# else
#     echo 1
# fi

# are_similar "$BLOB1" "$BLOB2"
# echo $?

# if (($(echo "$DIFF > 0.999" | bc -l) )); then
#     echo "same!"
# fi

# convert -metric ae TestCases/check.png TestCases/check.jpeg -trim -compare -format "%[distortion]" info:


#########################
# BLOB1="$(xxd -p TestCases/check.jpeg)"
# get_as_jpeg_blob "$BLOB1"
# BLOB1=$BLOB
# echo $BLOB

# BLOB2="$(xxd -p TestCases/check.png)"
# get_as_jpeg_blob "$BLOB2"
# BLOB2=$BLOB
# echo $BLOB

# if [[ "$BLOB1" != "$BLOB2" ]]; then
#     echo "not equal"
# fi
