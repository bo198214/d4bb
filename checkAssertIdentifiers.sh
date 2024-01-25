#!/bin/bash
# for generation of the identifiers use https://www.random.org/strings/
file_identifier=$(grep -e "\"[0-9]\{10\}" . -ro)
duplicates=$(echo "$file_identifier" | awk -F:\" '{print $2}' | sort | uniq -d)
if [ "$duplicates" ]
then
	for duplicate in $duplicates
	do
		echo "$file_identifier" | grep $duplicate
	done
fi
