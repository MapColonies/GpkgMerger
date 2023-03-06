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

OUTPUT_DATA_ARR=('gpkg')
OUTPUT_FORMAT_ARR=('jpeg' 'png')
BATCH_SIZE_ARR=(500 1000 2000 5000 10000 15000 20000)
THREAD_NUM_ARR=(1 3 5 8 10 15)

GEO=('geo' '33.8882,29.20989,36.22737,33.6244')
AREA1=('area1' '34.2663935002085,31.1786148130457,34.3258795317408,31.23180570002')
AREA2=('area2' '34.3872622999935,31.3605353131281,34.4079770002155,31.3883042230593')
AREA3=('area3' '34.4874343000325,31.5761480127462,34.5199694953832,31.6105553899892')
JORD=('Jord' '35.70419410,31.96472261,35.81542877,32.04025328')
SYRIA=('Syria' '35.941774115,32.80380351,36.01043588,32.87933150')
TZOR=('Tzor' '35.1837230,33.22952440,35.23727232,33.2968062')
MERGED=('merged' '33.8882,29.20989,36.22737,33.6244')
TILE=('tile' '33.8882,29.20989,36.22737,33.6244')

DATA_TO_CHECK=('GEO[@]' 'TZOR[@]' 'JORD[@]' 'SYRIA[@]' 'AREA1[@]' 'AREA2[@]' 'AREA3[@]')

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
    
    STARTTIME=$(date +%s)
    dotnet run --project MergerCli Program.cs $BATCH_SIZE $OUTPUT_FILE_TYPE gpkg $OUTPUT_FOLDER/gpkgs/${MERGED[0]}.gpkg ${MERGED[1]} gpkg $INPUT_FOLDER/gpkgs/${GEO[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${TZOR[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${JORD[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${SYRIA[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${AREA1[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${AREA2[0]}.gpkg gpkg $INPUT_FOLDER/gpkgs/${AREA3[0]}.gpkg >> run.txt
    RESULTS+=($(python3 TestMergerOutput/run_tests.py gpkg $OUTPUT_FOLDER/gpkgs/${MERGED[0]}.gpkg gpkg $TEST_RESULTS/gpkgs/${MERGED[0]}_${OUTPUT_FILE_TYPE}.gpkg))
    rm -f $OUTPUT_FOLDER/gpkgs/${MERGED[0]}.gpkg
    ENDTIME=$(date +%s)
    echo "It takes $(($ENDTIME - $STARTTIME)) seconds to complete run and check for merged..."

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
